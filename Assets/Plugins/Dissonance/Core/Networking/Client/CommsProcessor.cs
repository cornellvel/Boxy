using System;
using System.Collections.Generic;
using Dissonance.Extensions;

namespace Dissonance.Networking.Client
{
    /// <summary>
    /// Receives communications from other players and passes them onwards to the right place
    /// </summary>
    internal class CommsProcessor
    {
        #region fields and properties
        private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(CommsProcessor).Name);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(0.6);

        private readonly IClient _client;
        private readonly IReadonlyRoutingTable _peers;
        private readonly Rooms _rooms;
        private readonly ChannelCollection _channels;

        private readonly List<ReceivingState> _receiving = new List<ReceivingState>();

        private readonly List<OpenChannel> _tmpOpenChannelsBuffer = new List<OpenChannel>();
        private readonly List<int> _tmpCompositeIdBuffer = new List<int>();

        private ushort _sequenceNumber;
        #endregion

        #region constructor
        public CommsProcessor(IClient client, IReadonlyRoutingTable peers, Rooms rooms, ChannelCollection channels)
        {
            _client = client;
            _peers = peers;
            _rooms = rooms;
            _channels = channels;
        }
        #endregion

        public void Stop()
        {
            //Stop all current voice channels we're receiving
            foreach (var stats in _receiving)
            {
                var name = _peers.GetName(stats.PlayerId);
                if (name != null)
                    _client.OnPlayerStoppedSpeaking(name);
            }

            _receiving.Clear();
        }

        internal void PlayerLeftSession(string name, ushort id)
        {
            var state = FindState(id);

            if (state.Open)
                _client.OnPlayerStoppedSpeaking(name);

            FindState(id, true);
        }

        public void PlayerJoinedSession(string name, ushort id)
        {
            // Ensure there is a clean state object for this player
            FindState(id, true);
        }

        public void Update()
        {
            CheckTimeouts();
        }

        private void CheckTimeouts()
        {
            var now = DateTime.Now.Ticks;
            for (var i = _receiving.Count - 1; i >= 0; i--)
            {
                var stats = _receiving[i];
                if (stats.Open && now - stats.LastReceiptTicks > Timeout.Ticks)
                {
                    stats.Open = false;

                    var name = _peers.GetName(stats.PlayerId);
                    if (name == null)
                        Log.Warn("Player timed out, but their ID is unknown! Network ID = '{0}'", stats.PlayerId);
                    else
                        _client.OnPlayerStoppedSpeaking(name);
                }
            }
        }

        #region helpers
        private ReceivingState FindState(ushort senderId, bool reset = false)
        {
            if (senderId < _receiving.Count)
            {
                if (reset)
                    _receiving[senderId] = new ReceivingState();
            }
            else
            {
                while (_receiving.Count <= senderId)
                    _receiving.Add(new ReceivingState());
            }

            return _receiving[senderId];
        }

        [CanBeNull] private string FindRoomName(ushort roomId)
        {
            var name = _rooms.Name(roomId);
            if (name != null)
                return name;

            Log.Warn("Unknown room ID: {0}", roomId);
            return null;
        }

        private bool ChannelAddressesUs(ChannelBitField channel, ushort recipient)
        {
            if (channel.Type == ChannelType.Player)
                return recipient == _client.LocalId;

            return _rooms.Contains(recipient);
        }
        #endregion

        #region receive
        public void ReceiveVoiceData(ref PacketReader reader)
        {
            //Read header from voice packet
            byte options;
            ushort senderId, sequenceNumber, numChannels;
            reader.ReadVoicePacketHeader(out options, out senderId, out sequenceNumber, out numChannels);

            var channelSession = (options & 3);
            var playerName = _peers.GetName(senderId);

            //This packet could have arrived late (after the peer has left the session) or early (before the player has been registered). In either case discard the packet.
            if (playerName == null)
                return;

            var state = FindState(senderId);

            //Read channel states
            bool positional, allClosing, forceReset;
            ChannelPriority priority;
            ReadChannelStates(ref reader, state, numChannels, out positional, out allClosing, out forceReset, out priority);
    
            //Read encoded voice data and copy it into another buffer (the packet will be recycled immediately, so we can't keep this frame around)
            var frame = reader.ReadByteSegment().CopyTo(_client.ByteBufferPool.Get());

            //Update the statistics for the channel this data is coming in over
            var discardVoice = !UpdateSpeakerStates(state, allClosing, forceReset, channelSession, sequenceNumber, senderId, playerName);

            //Send the voice packet on as appropriate
            if (discardVoice)
                _client.ByteBufferPool.Put(frame.Array);
            else
                _client.OnVoicePacketReceived(new VoicePacket(playerName, priority, positional, frame, state.LocalSequenceNumber));

            //Indicate that the player stopped speaking
            if (!state.Open)
                _client.OnPlayerStoppedSpeaking(playerName);
        }

        private bool UpdateSpeakerStates(ReceivingState state, bool allClosing, bool forceReset, int channelSession, ushort sequenceNumber, ushort senderId, string playerName)
        {
            if (forceReset || state.ExpectedChannelSession != channelSession)
            {
                state.ExpectedChannelSession = channelSession;
                Log.Trace(
                    state.Open
                        ? "Channel Session has changed: {0} => {1} (Triggering forced playback reset)"
                        : "Channel Session has changed: {0} => {1}",
                    state.ExpectedChannelSession, channelSession
                );

                //If there is an open session force it closed (we will re-open it instantly)
                if (state.Open)
                {
                    _client.OnPlayerStoppedSpeaking(playerName);
                    state.Open = false;
                }
            }

            //If this player does not currently have an open channel, open it (and reset sequence numbers etc)
            if (!state.Open)
            {
                // check for old sequence numbers and discard
                if (state.BaseSequenceNumber.WrappedDelta(sequenceNumber) < 0)
                    return false;

                state.PlayerId = senderId;
                state.BaseSequenceNumber = sequenceNumber;
                state.LocalSequenceNumber = 0;
                state.LastReceiptTicks = DateTime.Now.Ticks;
                state.Open = true;

                _client.OnPlayerStartedSpeaking(playerName);
            }

            var sequenceDelta = state.BaseSequenceNumber.WrappedDelta(sequenceNumber);
            if (state.LocalSequenceNumber + sequenceDelta < 0)
            {
                // we must have received our first packet out of order
                // this "old" packet will give us a negative local sequence number, which will wrap when cast into the uint
                // discard the packet
                return false;
            }

            state.LastReceiptTicks = DateTime.Now.Ticks;
            state.LocalSequenceNumber = (uint)(state.LocalSequenceNumber + sequenceDelta);
            state.BaseSequenceNumber = sequenceNumber;
            state.Open = !allClosing;

            return true;
        }

        private void ReadChannelStates(ref PacketReader reader, ReceivingState state, ushort numChannels, out bool positional, out bool allClosing, out bool forceReset, out ChannelPriority priority)
        {
            positional = true;
            allClosing = true;
            int forcingReset = 0;
            priority = ChannelPriority.None;

            for (var i = 0; i < numChannels; i++)
            {
                byte channelBitfield;
                ushort channelRecipient;
                reader.ReadVoicePacketChannel(out channelBitfield, out channelRecipient);
                var channel = new ChannelBitField(channelBitfield);

                var compositeId = (int)(channel.Type) | channelRecipient << 8;
                _tmpCompositeIdBuffer.Add(compositeId);

                int previousSession;
                if (state.ExpectedPerChannelSessions.TryGetValue(compositeId, out previousSession))
                {
                    if (previousSession != channel.SessionId)
                        forcingReset++;
                }
                state.ExpectedPerChannelSessions[compositeId] = channel.SessionId;

                if (ChannelAddressesUs(channel, channelRecipient))
                {
                    if (!channel.IsPositional)
                        positional = false;

                    if (!channel.IsClosing)
                        allClosing = false;

                    if (channel.Priority > priority)
                        priority = channel.Priority;
                }
            }

            forceReset = forcingReset == numChannels;
            state.ClearChannels(_tmpCompositeIdBuffer);
            _tmpCompositeIdBuffer.Clear();
        }

        public void ReceiveTextData(ref PacketReader reader)
        {
            var packet = reader.ReadTextPacket(true);
            var recipientName = packet.RecipientType == ChannelType.Player ? _peers.GetName(packet.Recipient) : FindRoomName(packet.Recipient);
            var senderName = _peers.GetName(packet.Sender);

            if (packet.Text == null)
            {
                Log.Error("Received a text messaged from {0}(id={1}) with a null body", senderName, packet.Sender);
            }
            else if (senderName == null)
            {
                Log.Error("Received a text message from an unknown player id={0}, Message: {1}", packet.Recipient, packet.Text);
            }
            else if (recipientName == null)
            {
                Log.Error("Received a text message for an unknown {0} id={1}, Message: {2}", packet.RecipientType == ChannelType.Player ? "Player" : "Room", packet.Recipient, packet.Text);
            }
            else
            {
                _client.OnTextPacketReceived(new TextMessage(
                    senderName,
                    packet.RecipientType,
                    recipientName,
                    packet.Text
                ));
            }
        }
        #endregion

        #region send
        public void SendVoiceData(ArraySegment<byte> encodedAudio)
        {
            if (!_client.LocalId.HasValue)
            {
                Log.Warn("Not received ID from Dissonance server; skipping voice packet transmission");
                return;
            }

            //Get a copy of the currently open channels
            _tmpOpenChannelsBuffer.Clear();
            var sessionId = _channels.GetChannels(_tmpOpenChannelsBuffer);

            if (_tmpOpenChannelsBuffer.Count == 0)
            {
                Log.Debug("Trying to send a voice packet with no open channels");
                return;
            }

            //Write the voice and channel data into a network packet
            var packet = new PacketWriter(new ArraySegment<byte>(_client.ByteBufferPool.Get()))
                .WriteVoiceData(_client.SessionId, _client.LocalId.Value, ref _sequenceNumber, sessionId, _tmpOpenChannelsBuffer, encodedAudio)
                .Written;

            //Buffer up this packet to send ASAP
            _client.SendUnreliable(packet);

            //Clear up any channels which have been marked as "closing" (now that we know their status has been written into a packet)
            _channels.CleanClosingChannels();

        }

        public void SendTextData(string data, ChannelType type, string recipient)
        {
            if (!_client.LocalId.HasValue)
            {
                Log.Warn("Not received ID from Dissonance server; skipping text packet transmission");
                return;
            }

            var targetId = type == ChannelType.Player ? _peers.GetId(recipient) : recipient.ToRoomId();
            if (!targetId.HasValue)
            {
                Log.Warn("Unrecognised player name: '{0}'; skipping text packet transmission", recipient);
                return;
            }

            //Write the voice data into a network packet
            var packet = new PacketWriter(new ArraySegment<byte>(_client.ByteBufferPool.Get())).WriteTextPacket(_client.SessionId, _client.LocalId.Value, type, targetId.Value, data).Written;

            //Buffer up this packet to send ASAP
            _client.SendReliable(packet);
        }
        #endregion
    }
}
