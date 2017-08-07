using System;
using System.Collections.Generic;

namespace Dissonance.Networking.Client
{
    internal class ChannelCollection
        : IDisposable
    {
        #region fields and properties
        private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(ChannelCollection).Name);

        private readonly IReadonlyRoutingTable _peers;
        private readonly PlayerChannels _playerChannels;
        private readonly RoomChannels _roomChannels;

        private readonly List<OpenChannel> _openChannels = new List<OpenChannel>();

        private byte _sessionId;
        #endregion

        #region constructor
        public ChannelCollection(IReadonlyRoutingTable peers, PlayerChannels playerChannels, RoomChannels roomChannels)
        {
            _peers = peers;
            _playerChannels = playerChannels;
            _roomChannels = roomChannels;

            playerChannels.OpenedChannel += OpenPlayerChannel;
            playerChannels.ClosedChannel += ClosePlayerChannel;
            roomChannels.OpenedChannel += OpenRoomChannel;
            roomChannels.ClosedChannel += CloseRoomChannel;
            
            //There may already be some channels which were created before we created those events, run through them all now so we're up to date
            foreach (var playerChannel in playerChannels)
                OpenPlayerChannel(playerChannel.Value.TargetId, playerChannel.Value.Properties);
            foreach (var roomChannel in roomChannels)
                OpenRoomChannel(roomChannel.Value.TargetId, roomChannel.Value.Properties);
        }
        #endregion

        public void Dispose()
        {
            _openChannels.Clear();

            _playerChannels.OpenedChannel -= OpenPlayerChannel;
            _playerChannels.ClosedChannel -= ClosePlayerChannel;
            _roomChannels.OpenedChannel -= OpenRoomChannel;
            _roomChannels.ClosedChannel -= CloseRoomChannel;
        }

        /// <summary>
        /// Get all the open channels
        /// </summary>
        /// <param name="channels"></param>
        /// <returns>The ID of this set of channels (changes every time all channels are closed)</returns>
        public byte GetChannels(IList<OpenChannel> channels)
        {
            lock (_openChannels)
                for (var i = 0; i < _openChannels.Count; i++)
                    channels.Add(_openChannels[i]);

            return _sessionId;
        }

        public void CleanClosingChannels()
        {
            lock (_openChannels)
            {
                for (var i = _openChannels.Count - 1; i >= 0; i--)
                    if (_openChannels[i].IsClosing)
                        _openChannels.RemoveAt(i);
            }
        }

        private void OpenChannel(ChannelType type, ChannelProperties config, ushort recipient)
        {
            lock (_openChannels)
            {
                //Check if we have a closing channel which we're now trying to re-open
                var reopoened = false;
                for (var i = 0; i < _openChannels.Count; i++)
                {
                    var c = _openChannels[i];

                    if (c.Type == type && ReferenceEquals(c.Config, config) && c.Recipient == recipient)
                    {
                        _openChannels[i] = c.AsOpen();
                        reopoened = true;
                        break;
                    }
                }

                //Failed to find a channel to re-open so just add a new one
                if (!reopoened)
                    _openChannels.Add(new OpenChannel(type, 0, config, false, recipient));
            }
        }

        private void CloseChannel(ChannelType type, ushort id, ChannelProperties properties)
        {
            lock (_openChannels)
            {
                var allClosing = true;
                int index;

                //Find the channel and change it to a closing version of the channel
                //As we go, accumulate a flag indicating if *all* channels are currently closing
                for (index = 0; index < _openChannels.Count; index++)
                {
                    var channel = _openChannels[index];
                    if (!channel.IsClosing && channel.Type == type && channel.Recipient == id && ReferenceEquals(channel.Config, properties))
                    {
                        _openChannels[index] = channel.AsClosing();
                        break;
                    }
                    else
                        allClosing &= channel.IsClosing;
                }

                //Finish off accumulating that flag
                for (; index < _openChannels.Count; index++)
                    allClosing &= _openChannels[index].IsClosing;

                //All channels are closing, bump up the session ID so the receiving end can tell
                if (allClosing)
                    unchecked { _sessionId++; }
            }
        }

        private void OpenPlayerChannel(string player, ChannelProperties config)
        {
            var id = _peers.GetId(player);
            if (id == null)
            {
                Log.Warn("Unrecognized player ID '{0}'", player);
                return;
            }

            OpenChannel(ChannelType.Player, config, id.Value);
        }

        private void ClosePlayerChannel(string player, ChannelProperties config)
        {
            var id = _peers.GetId(player);
            if (id == null)
            {
                Log.Warn("Unrecognized player name '{0}'", player);
                return;
            }

            CloseChannel(ChannelType.Player, id.Value, config);
        }

        private void OpenRoomChannel(string roomName, ChannelProperties config)
        {
            OpenChannel(ChannelType.Room, config, roomName.ToRoomId());
        }

        private void CloseRoomChannel(string roomName, ChannelProperties config)
        {
            CloseChannel(ChannelType.Room, roomName.ToRoomId(), config);
        }
    }
}
