using System.Linq;

namespace Dissonance.Networking.Client
{
    internal class PeerCollection
    {
        #region fields and properties
        private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(PeerCollection).Name);

        private readonly IClient _client;

        private readonly RoutingTable _tempOldRoutingTable = new RoutingTable();
        private readonly RoutingTable _tempNewRoutingTable = new RoutingTable();

        private readonly RoutingTable _playerIds = new RoutingTable();
        [NotNull]public IReadonlyRoutingTable RoutingTable
        {
            get { return _playerIds; }
        }

        public ushort? LocalPeerId { get; private set; }
        #endregion

        #region constructor
        public PeerCollection(IClient client)
        {
            _client = client;
        }
        #endregion

        /// <summary>
        /// Remove all players from the session
        /// </summary>
        public void Clear()
        {
            var players = _playerIds.Items.ToArray();
            foreach (var player in players)
                _client.OnPlayerLeft(player);
            _playerIds.Clear();
        }

        public void ReceivePlayerRoutingUpdate(ref PacketReader reader)
        {
            //This method allocates a few temporary lists. This is ok because it only happens very rarely - when a player joins or leaves the session
            Log.Debug("Received player routing table");

            //Deserialize the new session state (this is essentially a list of all players in the session)
            _tempNewRoutingTable.Clear();
            _tempNewRoutingTable.Deserialize(ref reader);

            //Check our own ID. If this isn't present something is wrong and bail out.
            LocalPeerId = _tempNewRoutingTable.GetId(_client.PlayerName);
            if (!LocalPeerId.HasValue)
            {
                Log.Warn("Received player routing update, cannot find self ID");
                return;
            }

            // Find which players are no longer in the session. Do this before copying the information into _playerIds,
            // this ensures the old player IDs can still be looked up inside the OnPlayerLeft method
            foreach (var player in _playerIds.Items)
                if (!_tempNewRoutingTable.GetId(player).HasValue)
                    _client.OnPlayerLeft(player);

            // Clone the old state so we know who has joined the session
            _tempOldRoutingTable.CopyFrom(_playerIds);

            //Copy info from temp into ID table
            _playerIds.CopyFrom(_tempNewRoutingTable);

            // Find which players newly joined the session. Do this after copying the information into _playerIds,
            // this ensures the new player IDs can be looked up inside the OnPlayerJoined methods
            foreach (var player in _tempNewRoutingTable.Items)
                if (!_tempOldRoutingTable.GetId(player).HasValue)
                        _client.OnPlayerJoined(player);

            //Clean up after ourselves
            _tempOldRoutingTable.Clear();
            _tempNewRoutingTable.Clear();
        }
    }
}
