using System;
using System.Collections.Generic;
using Dissonance.Networking.Server;

namespace Dissonance.Networking
{
    public enum ServerState
    {
        Ok,
        Error
    }

    public abstract class BaseServer<TServer, TClient, TPeer>
        : IServer<TPeer>
        where TPeer : IEquatable<TPeer>
        where TServer : BaseServer<TServer, TClient, TPeer>
        where TClient : BaseClient<TServer, TClient, TPeer>
    {
        #region fields and properties
        protected readonly Log Log;

        private bool _disconnected;

        private readonly PacketRouter<TPeer> _packetRouter;
        private readonly ClientCollection<TPeer> _clients;

        private readonly ArraySegment<byte> _handshakeResponse;

        internal TrafficCounter RecvClientState { get; private set; }
        internal TrafficCounter RecvVoiceData { get; private set; }
        internal TrafficCounter RecvTextData { get; private set; }
        internal TrafficCounter RecvHandshakeRequest { get; private set; }

        internal TrafficCounter SentTraffic { get; private set; }

        private readonly uint _sessionId;
        #endregion

        protected BaseServer()
        {
            Log = Logs.Create(LogCategory.Network, GetType().Name);

            RecvClientState = new TrafficCounter();
            RecvVoiceData = new TrafficCounter();
            RecvTextData = new TrafficCounter();
            RecvHandshakeRequest = new TrafficCounter();
            SentTraffic = new TrafficCounter();

            _sessionId = unchecked((uint)new Random().Next());
            Log.Debug("Session Id = {0}", _sessionId);

            _packetRouter = new PacketRouter<TPeer>(this);
            _clients = new ClientCollection<TPeer>(this, _sessionId);

            _handshakeResponse = new PacketWriter(new byte[21]).WriteHandshakeResponse(_sessionId).Written;
        }

        /// <summary>
        /// Perform any initial work required to connect
        /// </summary>
        public virtual void Connect()
        {
            Log.Info("Connected");
        }

        /// <summary>
        /// Perform any teardown work required to disconnect
        /// </summary>
        public virtual void Disconnect()
        {
            if (_disconnected)
                return;

            _disconnected = true;

            _clients.Clear();

            Log.Info("Disconnected");
        }

        /// <summary>
        /// This must be called by the extending network integration implementation when a client disconnects from the session
        /// </summary>
        /// <param name="connection"></param>
        protected void ClientDisconnected(TPeer connection)
        {
            _clients.RemoveClient(connection);
        }

        private void SendErrorWrongSession(TPeer peer, uint session)
        {
            var writer = new PacketWriter(new byte[7]);
            writer.WriteErrorWrongSession(session);

            SendReliable(peer, writer.Written);
        }

        // ReSharper disable once VirtualMemberNeverOverridden.Global (Justification: Public API)
        public virtual ServerState Update()
        {
            if (_disconnected)
                return ServerState.Error;

            ReadMessages();
            return ServerState.Ok;
        }

        #region abstracts
        /// <summary>
        /// Read messages (call NetworkReceivedPacket with all messages)
        /// </summary>
        protected abstract void ReadMessages();

        /// <summary>
        /// Send a control packet (reliable, in-order) to the given destination
        /// </summary>
        /// <param name="connection">Destination</param>
        /// <param name="packet">Packet to send</param>
        protected abstract void SendReliable(TPeer connection, ArraySegment<byte> packet);

        /// <summary>
        /// Send an unreliable packet (unreliable, unordered) to the given destination
        /// </summary>
        /// <param name="connection">Destination</param>
        /// <param name="packet">Packet to send</param>
        protected abstract void SendUnreliable(TPeer connection, ArraySegment<byte> packet);

        void IServer<TPeer>.SendUnreliable(TPeer connection, ArraySegment<byte> packet)
        {
            SentTraffic.Update(packet.Count);

            SendUnreliable(connection, packet);
        }

        void IServer<TPeer>.SendReliable(TPeer connection, ArraySegment<byte> packet)
        {
            SentTraffic.Update(packet.Count);

            SendReliable(connection, packet);
        }
        #endregion

        #region packet processing
        /// <summary>
        /// Receive a packet from the network for dissonance
        /// </summary>
        /// <param name="source">An integer identifying where this packet came from (same ID will be used for sending)</param>
        /// <param name="data">Packet received</param>
        public void NetworkReceivedPacket(TPeer source, ArraySegment<byte> data)
        {
            var reader = new PacketReader(data);

            var magic = reader.ReadUInt16();
            if (magic != PacketWriter.Magic)
            {
                Log.Warn("Received packet with incorrect magic number. Expected {0}, got {1}", PacketWriter.Magic, magic);
                return;
            }

            var header = (MessageTypes)reader.ReadByte();

            if (header != MessageTypes.HandshakeRequest)
            {
                var session = reader.ReadUInt32();
                if (session != _sessionId)
                {
                    Log.Warn("Received a packet with incorrect session ID. Expected {0}, got {1}. Resetting client.", _sessionId, session);
                    SendErrorWrongSession(source, _sessionId);
                    return;
                }
            }

            switch (header)
            {
                case MessageTypes.ClientState:
                    RecvClientState.Update(data.Count);
                    _clients.ProcessClientState(source, ref reader);
                    break;

                case MessageTypes.PlayerRoutingUpdate:
                    Log.Error("Received a routing update (this should only ever be received by the client)");
                    break;

                case MessageTypes.VoiceData:
                    RecvVoiceData.Update(data.Count);
                    _packetRouter.ProcessVoiceData(source, ref reader);
                    break;

                case MessageTypes.TextData:
                    RecvTextData.Update(data.Count);
                    _packetRouter.ProcessTextData(ref reader);
                    break;

                case MessageTypes.HandshakeRequest:
                    RecvHandshakeRequest.Update(data.Count);
                    ClientDisconnected(source); // disconnect existing peers on this connection
                    ((IServer<TPeer>)this).SendReliable(source, _handshakeResponse);
                    break;

                case MessageTypes.HandshakeResponse:
                    Log.Error("Received a handshake response (this should only ever be received by the client)");
                    break;

                case MessageTypes.ErrorWrongSession:
                    Log.Error("Received wrong session error from client (this should only ever be received by the client)");
                    break;

                default:
                    Log.Error("Ignoring a packet with an unknown header: '{0}'", header);
                    break;
            }
        }
        #endregion

        /// <summary>
        /// Called whenever a new client joins the session
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="client"></param>
        protected virtual void AddClient(TPeer peer, ClientInfo client)
        {
        }

        #region IServer explicit implementation
        void IServer<TPeer>.AddClient(TPeer source, ClientInfo client)
        {
            AddClient(source, client);
        }

        int IServer<TPeer>.GetConnectionsInRoom(ushort room, List<TPeer> recipientsBuffer)
        {
            return _clients.GetConnectionsInRoom(room, recipientsBuffer);
        }

        bool IServer<TPeer>.GetConnectionToPlayer(ushort player, out TPeer connection)
        {
            return _clients.GetConnectionToPlayer(player, out connection);
        }
        #endregion
    }

    /// <summary>
    /// Information about a client in a network session
    /// </summary>
    public class ClientInfo
    {
        private readonly string _playerName;
        private readonly ushort _playerId;
        private readonly List<ushort> _rooms;

        /// <summary>
        /// Name of this client (as specified by the DissonanceComms component for the client)
        /// </summary>
        [NotNull] public string PlayerName
        {
            get { return _playerName; }
        }

        /// <summary>
        /// Unique ID of this client
        /// </summary>
        public ushort PlayerId
        {
            get { return _playerId; }
        }

        /// <summary>
        /// List of rooms this client is listening to
        /// </summary>
        [NotNull] public List<ushort> Rooms
        {
            get { return _rooms; }
        }

        public ClientInfo(string playerName, ushort playerId)
        {
            _playerName = playerName;
            _playerId = playerId;
            _rooms = new List<ushort>();
        }
    }
}
