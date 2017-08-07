using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Dissonance.Networking
{
    public abstract class BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam>
        : MonoBehaviour, ICommsNetwork, ICommsNetworkState
        where TPeer: IEquatable<TPeer>
        where TServer: BaseServer<TServer, TClient, TPeer>
        where TClient: BaseClient<TServer, TClient, TPeer>
    {
        #region States
        private interface IState
        {
            ConnectionStatus Status { get; }
            void Enter();
            void Update();
            void Exit();
        }

        private class Idle : IState
        {
            private readonly BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> _net;

            public Idle(BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> net)
            {
                _net = net;
            }

            public ConnectionStatus Status
            {
                get { return ConnectionStatus.Disconnected; }
            }

            public void Enter()
            {
                _net.Mode = NetworkMode.None;
            }

            public void Update() { }
            public void Exit() { }
        }

        private class Session : IState
        {
            private readonly TClientParam _clientParameter;
            private readonly TServerParam _serverParameter;
            private readonly NetworkMode _mode;
            private readonly BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> _net;

            private float _reconnectionAttemptInterval;
            private DateTime _lastReconnectionAttempt;

            public Session(
                BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> net,
                NetworkMode mode, TServerParam serverParameter, TClientParam clientParameter)
            {
                _net = net;
                _clientParameter = clientParameter;
                _serverParameter = serverParameter;
                _mode = mode;
            }

            public ConnectionStatus Status
            {
                get
                {
                    var serverOk = !_mode.IsServerEnabled() || _net.Server != null;
                    var clientOk = !_mode.IsClientEnabled() || (_net.Client != null && _net.Client.IsConnected);

                    if (serverOk && clientOk)
                        return ConnectionStatus.Connected;

                    return ConnectionStatus.Degraded;
                }
            }
            
            public void Enter()
            {
                _net.Log.Debug("Starting network session as {0}", _mode);

                if (_mode.IsServerEnabled())
                    StartServer();

                if (_mode.IsClientEnabled())
                    StartClient();

                _net.Mode = _mode;
            }

            public void Update()
            {
                if (_mode.IsServerEnabled())
                {
                    var state = _net.Server.Update();

                    if (state == ServerState.Error)
                    {
                        _net.Log.Trace("Restarting server");

                        _net.StopServer();
                        StartServer();
                    }
                }

                if (_mode.IsClientEnabled())
                {
                    if (_net.Client != null)
                    {
                        var state = _net.Client.Update();

                        if (state == ClientStatus.Error)
                            _net.StopClient();
                        else
                            _reconnectionAttemptInterval = Math.Max(0, _reconnectionAttemptInterval - Time.deltaTime);

                    }

                    if (_net.Client == null && ShouldAttemptReconnect())
                    {
                        _net.Log.Trace("Restarting client");

                        StartClient();
                        _reconnectionAttemptInterval = Math.Min(3, _reconnectionAttemptInterval + 0.5f);
                    }
                }
            }

            public void Exit()
            {
                _net.Log.Debug("Closing network session");

                if (_net.Client != null)
                    _net.StopClient();

                if (_net.Server != null)
                    _net.StopServer();
            }

            private void StartServer()
            {
                _net.StartServer(_serverParameter);
            }

            private void StartClient()
            {
                _net.StartClient(_clientParameter);
                _lastReconnectionAttempt = DateTime.Now;
            }

            private bool ShouldAttemptReconnect()
            {
                return (DateTime.Now - _lastReconnectionAttempt).TotalSeconds >= _reconnectionAttemptInterval;
            }
        }
        #endregion

        private readonly Queue<IState> _nextStates;
        private IState _state;
        private bool _initialized;
        private NetworkMode _mode;

        protected TServer Server { get; private set; }
        protected TClient Client { get; private set; }

        protected readonly Log Log;

        public string PlayerName { get; private set; }
        public Rooms Rooms { get; private set; }
        public PlayerChannels PlayerChannels { get; private set; }
        public RoomChannels RoomChannels { get; private set; }

        public event Action<NetworkMode> ModeChanged;
        public event Action<string> PlayerJoined;
        public event Action<string> PlayerLeft;
        public event Action<VoicePacket> VoicePacketReceived;
        public event Action<TextMessage> TextPacketReceived;
        public event Action<string> PlayerStartedSpeaking;
        public event Action<string> PlayerStoppedSpeaking;

        public bool IsInitialized
        {
            get { return _initialized; }
        }

        public ConnectionStatus Status
        {
            get { return _state.Status; }
        }

        public NetworkMode Mode
        {
            get { return _mode; }
            private set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnModeChanged(value);
                }
            }
        }

        protected BaseCommsNetwork()
        {
            Log = Logs.Create(LogCategory.Network, GetType().Name);

            _nextStates = new Queue<IState>();
            _mode = NetworkMode.None;
            _state = new Idle(this);
        }

        protected abstract TServer CreateServer(TServerParam connectionParameters);
        protected abstract TClient CreateClient(TClientParam connectionParameters);
        protected virtual void Initialize() { }

        public void Initialize(string playerName, Rooms rooms, PlayerChannels playerChannels, RoomChannels roomChannels)
        {
            if (playerName == null)
                throw new ArgumentNullException("playerName");
            if (rooms == null)
                throw new ArgumentNullException("rooms");
            if (playerChannels == null)
                throw new ArgumentNullException("playerChannels");
            if (roomChannels == null)
                throw new ArgumentNullException("roomChannels");

            PlayerName = playerName;
            Rooms = rooms;
            PlayerChannels = playerChannels;
            RoomChannels = roomChannels;

            Initialize();

            _initialized = true;
        }
        
        protected virtual void Update()
        {
            if (!_initialized)
                return;

            LoadState();
            _state.Update();
        }

        private void LoadState()
        {
            while (_nextStates.Count > 0)
                ChangeState(_nextStates.Dequeue());
        }

        protected virtual void OnDisable()
        {
            Stop();
            LoadState();
        }

        public void Stop()
        {
            _nextStates.Enqueue(new Idle(this));
        }

        protected void RunAsHost(TServerParam serverParameters, TClientParam clientParameters)
        {
            _nextStates.Enqueue(new Session(this, NetworkMode.Host, serverParameters, clientParameters));
        }

        protected void RunAsClient(TClientParam clientParameters)
        {
            _nextStates.Enqueue(new Session(this, NetworkMode.Client, default(TServerParam), clientParameters));
        }

        protected void RunAsDedicatedServer(TServerParam serverParameters)
        {
            _nextStates.Enqueue(new Session(this, NetworkMode.DedicatedServer, serverParameters, default(TClientParam)));
        }

        private void ChangeState(IState newState)
        {
            _state.Exit();
            _state = newState;
            _state.Enter();
        }

        private void StartServer(TServerParam connectParams)
        {
            if (Server != null)
            {
                throw Log.CreatePossibleBugException(
                    "Attempted to start the network server while the server is already running",
                    "680CB0B1-1F2C-4EB2-A249-3EDD513354B9"
                );
            }

            Server = CreateServer(connectParams);
            Server.Connect();
        }

        private void StopServer()
        {
            if (Server == null)
            {
                throw Log.CreatePossibleBugException(
                    "Attempted to stop the network server while the server is not running",
                    "BCA52BAC-DE86-4037-9C7B-508D1798E50B"
                );
            }

            try
            {
                Server.Disconnect();
            }
            catch (Exception e)
            {
                Log.Error("Encountered error shutting down server: '{0}'", e.Message);
            }

            Server = null;
        }

        private void StartClient(TClientParam connectParams)
        {
            if (Client != null)
            {
                throw Log.CreatePossibleBugException(
                    "Attempted to start client while the client is already running",
                    "0AEB8FC5-025F-46F5-969A-B792D2E84626"
                );
            }

            Client = CreateClient(connectParams);

            Log.Trace("Subscribing to client events");

            Client.PlayerJoined += OnPlayerJoined;
            Client.PlayerLeft += OnPlayerLeft;
            Client.VoicePacketReceived += OnVoicePacketReceived;
            Client.TextMessageReceived += OnTextPacketReceived;
            Client.PlayerStartedSpeaking += OnPlayerStartedSpeaking;
            Client.PlayerStoppedSpeaking += OnPlayerStoppedSpeaking;

            Client.Connect();
        }

        private void StopClient()
        {
            if (Client == null)
            {
                throw Log.CreatePossibleBugException(
                    "Attempted to stop the client while the client is not running",
                    "F44A101A-6EF3-4668-9E29-2447B0137921"
                );
            }

            try
            {
                Client.Disconnect();
            }
            catch (Exception e)
            {
                Log.Error("Encountered error shutting down client: '{0}'", e.Message);
            }

            Log.Trace("Unsubscribing from client events");

            Client.PlayerJoined -= OnPlayerJoined;
            Client.PlayerLeft -= OnPlayerLeft;
            Client.VoicePacketReceived -= OnVoicePacketReceived;
            Client.TextMessageReceived -= OnTextPacketReceived;
            Client.PlayerStartedSpeaking -= OnPlayerStartedSpeaking;
            Client.PlayerStoppedSpeaking -= OnPlayerStoppedSpeaking;

            Client = null;
        }

        public void SendVoice(ArraySegment<byte> data)
        {
            if (Client != null)
                Client.SendVoiceData(data);
        }

        public void SendText(string data, ChannelType recipientType, string recipientId)
        {
            if (Client != null)
                Client.SendTextData(data, recipientType, recipientId);
        }

        private void OnPlayerJoined(string obj)
        {
            var handler = PlayerJoined;
            if (handler != null) handler(obj);
        }

        private void OnPlayerLeft(string obj)
        {
            var handler = PlayerLeft;
            if (handler != null) handler(obj);
        }

        private void OnVoicePacketReceived(VoicePacket obj)
        {
            var handler = VoicePacketReceived;
            if (handler != null) handler(obj);
        }

        private void OnTextPacketReceived(TextMessage obj)
        {
            var handler = TextPacketReceived;
            if (handler != null) handler(obj);
        }

        private void OnPlayerStartedSpeaking(string obj)
        {
            var handler = PlayerStartedSpeaking;
            if (handler != null) handler(obj);
        }

        private void OnPlayerStoppedSpeaking(string obj)
        {
            var handler = PlayerStoppedSpeaking;
            if (handler != null) handler(obj);
        }

        private void OnModeChanged(NetworkMode obj)
        {
            var handler = ModeChanged;
            if (handler != null) handler(obj);
        }

        /// <summary>
        /// Draw an inspector GUI for this network
        /// </summary>
        public void OnInspectorGui()
        {
#if UNITY_EDITOR
            string mode = "None";
            if (Mode == NetworkMode.Host)
                mode = "Server & Client";
            else if (Mode == NetworkMode.Client)
                mode = "Client";
            else if (Mode == NetworkMode.DedicatedServer)
                mode = "Server";

            EditorGUILayout.LabelField("Mode", mode);

            if (!Mode.IsServerEnabled() && !Mode.IsClientEnabled())
                return;
            
            EditorGUILayout.LabelField("Connection Status", Status.ToString());

            EditorGUILayout.LabelField("Received");
            EditorGUI.indentLevel++;
            try
            {
                if (Client != null)
                {
                    EditorGUILayout.LabelField("Client");
                    EditorGUI.indentLevel++;
                    try
                    {
                        EditorGUILayout.LabelField("Handshake", Client.RecvHandshakeResponse.ToString());
                        EditorGUILayout.LabelField("Routing", Client.RecvRoutingUpdate.ToString());
                        EditorGUILayout.LabelField("Text", Client.RecvTextData.ToString());
                        EditorGUILayout.LabelField("Voice", Client.RecvVoiceData.ToString());

                        int totalPackets, totalBytes, totalBytesPerSecond;
                        TrafficCounter.Combine(out totalPackets, out totalBytes, out totalBytesPerSecond, Client.RecvHandshakeResponse, Client.RecvTextData, Client.RecvRoutingUpdate, Client.RecvVoiceData);
                        EditorGUILayout.LabelField("TOTAL", TrafficCounter.Format(totalPackets, totalBytes, totalBytesPerSecond));
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }

                if (Server != null)
                {
                    EditorGUILayout.LabelField("Server");
                    EditorGUI.indentLevel++;
                    try
                    {
                        EditorGUILayout.LabelField("Handshake", Server.RecvHandshakeRequest.ToString());
                        EditorGUILayout.LabelField("CState", Server.RecvClientState.ToString());
                        EditorGUILayout.LabelField("Text", Server.RecvTextData.ToString());
                        EditorGUILayout.LabelField("Voice", Server.RecvVoiceData.ToString());

                        int totalPackets, totalBytes, totalBytesPerSecond;
                        TrafficCounter.Combine(out totalPackets, out totalBytes, out totalBytesPerSecond, Server.RecvHandshakeRequest, Server.RecvTextData, Server.RecvClientState, Server.RecvVoiceData);
                        EditorGUILayout.LabelField("TOTAL", TrafficCounter.Format(totalPackets, totalBytes, totalBytesPerSecond));
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
            }
            finally
            {
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.LabelField("Sent");
            EditorGUI.indentLevel++;
            try
            {
                if (Server != null)
                    EditorGUILayout.LabelField("Server", Server.SentTraffic.ToString());
                if (Client != null)
                    EditorGUILayout.LabelField("Client", Client.SentTraffic.ToString());
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
#endif
        }
    }
}
