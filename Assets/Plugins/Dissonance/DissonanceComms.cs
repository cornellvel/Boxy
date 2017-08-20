using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dissonance.Audio.Capture;
using Dissonance.Audio.Codecs.Opus;
using Dissonance.Audio.Playback;
using Dissonance.Config;
using Dissonance.Datastructures;
using Dissonance.Networking;
using Dissonance.VAD;
using NAudio.Wave;
using UnityEngine;

namespace Dissonance
{
    /// <summary>
    ///     The central Dissonance Voice Comms component.
    ///     Place one of these on a voice comm entity near the root of your scene.
    /// </summary>
    /// <remarks>
    ///     Handles recording the local player's microphone and sending the data to the network.
    ///     Handles managing the playback entities for the other users on the network.
    ///     Provides the API for opening and closing channels.
    /// </remarks>
    public sealed class DissonanceComms
        : MonoBehaviour, IPriorityManager, IAccessTokenCollection, IChannelPriorityProvider
    {
        #region fields
        private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(DissonanceComms).Name);

        private bool _started;

        private readonly List<IVoiceActivationListener> _activationListeners = new List<IVoiceActivationListener>();
        private readonly Rooms _rooms;
        private readonly PlayerChannels _playerChannels;
        private readonly RoomChannels _roomChannels;
        private readonly TextChat _text;

        private readonly Dictionary<string, IDissonancePlayer> _unlinkedPlayerTrackers = new Dictionary<string, IDissonancePlayer>();

        private LocalVoicePlayerState _localPlayerState;
        private readonly Dictionary<string, VoicePlayerState> _playersLookup;
        private readonly List<VoicePlayerState> _players;
        private readonly ReadOnlyCollection<VoicePlayerState> _playersReadOnly;

        private readonly Pool<VoicePlayback> _playbackPool;

        private ICommsNetwork _net;
        private EncoderPipeline _transmissionPipeline;
        private uint _decoderFrameSize;
        private int _decoderSampleRate;
        private string _localPlayerName;

        public MicrophoneCapture MicCapture { get; private set; }

        [SerializeField]private bool _isMuted;
        /// <summary>
        /// Get or set if the local player is muted (prevented from sending any voice transmissions)
        /// </summary>
        public bool IsMuted
        {
            get { return _isMuted; }
            set { _isMuted = value; }
        }

        [SerializeField]private VoicePlayback _playbackPrefab;
        /// <summary>
        /// Get or set the prefab to use for voice playback (may only be set before this component Starts)
        /// </summary>
        public VoicePlayback PlaybackPrefab
        {
            get { return _playbackPrefab; }
            set
            {
                if (_started)
                    throw Log.CreateUserErrorException("Cannot set playback prefab when the component has been started", "directly setting the 'PlaybackPrefab' property too late", "https://placeholder-software.co.uk/dissonance/docs/Reference/Components/Dissonance-Comms.md", "A0796DA8-A0BC-49E4-A1B3-F0AA0F51BAA0");

                _playbackPrefab = value;
            }
        }

        [SerializeField]private string _micName;
        /// <summary>
        /// Get or set the microphone device name to use for voice capture (may only be set before this component Starts)
        /// </summary>
        public string MicrophoneName
        {
            get { return _micName; }
            set
            {
                if (_started)
                    throw Log.CreateUserErrorException("Cannot set mic name when the component has been started", "directly setting the 'MicrophoneName' property too late", "https://placeholder-software.co.uk/dissonance/docs/Reference/Components/Dissonance-Comms.md", "48B14B37-41E8-4626-9BD6-3C623678539B");

                _micName = value;
            }
        }

        [SerializeField]private ChannelPriority _playerPriority = ChannelPriority.Default;
        /// <summary>
        /// The default priority to use for this player if a broadcast trigger does not specify a priority
        /// </summary>
        public ChannelPriority PlayerPriority
        {
            get { return _playerPriority; }
            set { _playerPriority = value; }
        }

        ChannelPriority IChannelPriorityProvider.DefaultChannelPriority
        {
            get { return PlayerPriority; }
            set { PlayerPriority = value; }
        }

        public event Action<string> LocalPlayerNameChanged;

        private ChannelPriority _topPrioritySpeaker = ChannelPriority.None;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local (Justification: Confuses unity serialization)
        [SerializeField]private TokenSet _tokens = new TokenSet();

        // ReSharper disable EventNeverSubscribedTo.Global (Justification: Part of public API)
        public event Action<VoicePlayerState> OnPlayerJoinedSession;
        public event Action<VoicePlayerState> OnPlayerLeftSession;
        public event Action<VoicePlayerState> OnPlayerStartedSpeaking;
        public event Action<VoicePlayerState> OnPlayerStoppedSpeaking;
        // ReSharper restore EventNeverSubscribedTo.Global
        #endregion

        public DissonanceComms()
        {
            _playbackPool = new Pool<VoicePlayback>(6, CreatePlayback);
            _rooms = new Rooms();
            _playerChannels = new PlayerChannels(this);
            _roomChannels = new RoomChannels(this);
            _text = new TextChat(() => _net);
            _players = new List<VoicePlayerState>();
            _playersReadOnly = new ReadOnlyCollection<VoicePlayerState>(_players);
            _playersLookup = new Dictionary<string, VoicePlayerState>();

            _rooms.JoinedRoom += name => Log.Debug("Joined chat room '{0}'", name);
            _rooms.LeftRoom += name => Log.Debug("Left chat room '{0}'", name);

            _playerChannels.OpenedChannel += OnChannelOpenedOrClosed;
            _roomChannels.OpenedChannel += OnChannelOpenedOrClosed;
            _playerChannels.ClosedChannel += OnChannelOpenedOrClosed;
            _roomChannels.ClosedChannel += OnChannelOpenedOrClosed;

            _playerChannels.OpenedChannel += (id, _) => {
                Log.Debug("Opened channel to player '{0}'", id);
            };

            _playerChannels.ClosedChannel += (id, _) => {
                Log.Debug("Closed channel to player '{0}'", id);
            };

            _roomChannels.OpenedChannel += (id, _) => {
                Log.Debug("Opened channel to room '{0}'", id);
            };

            _roomChannels.ClosedChannel += (id, _) => {
                Log.Debug("Closed channel to room '{0}'", id);
            };
        }

        #region properties
        /// <summary>
        /// Get or set the local player name (may only be set before this component starts)
        /// </summary>
        public string LocalPlayerName
        {
            get { return _localPlayerName; }
            set
            {
                if (_localPlayerName == value)
                    return;

                if (_started)
                    throw Log.CreateUserErrorException("Cannot set player name when the component has been started", "directly setting the 'LocalPlayerName' property too late", "https://placeholder-software.co.uk/dissonance/docs/Reference/Components/Dissonance-Comms.md", "58973EDF-42B5-4FF1-BE01-FFF28300A97E");

                _localPlayerName = value;
                OnLocalPlayerNameChanged(value);
            }
        }

        /// <summary>
        /// Get a value indicating if Dissonance has successfully connected to a voice network yet
        /// </summary>
        public bool IsNetworkInitialized
        {
            get { return _net.Status == ConnectionStatus.Connected; }
        }
        
        /// <summary>
        /// Get an object to control which rooms the local player is listening to
        /// </summary>
        [NotNull] public Rooms Rooms
        {
            get { return _rooms; }
        }

        /// <summary>
        /// Get an object to control channels to other players
        /// </summary>
        [NotNull] public PlayerChannels PlayerChannels
        {
            get { return _playerChannels; }
        }

        /// <summary>
        /// Get an object to control channels to rooms (transmitting)
        /// </summary>
        [NotNull] public RoomChannels RoomChannels
        {
            get { return _roomChannels; }
        }

        /// <summary>
        /// Get an object to send and receive text messages
        /// </summary>
        [NotNull] public TextChat Text
        {
            get { return _text; }
        }

        /// <summary>
        /// Get a list of states of all players in the Dissonance voice session
        /// </summary>
        [NotNull] public ReadOnlyCollection<VoicePlayerState> Players
        {
            get { return _playersReadOnly; }
        }

        /// <summary>
        /// Get the priority of the current highest priority speaker
        /// </summary>
        public ChannelPriority TopPrioritySpeaker
        {
            get { return _topPrioritySpeaker; }
        }

        ChannelPriority IPriorityManager.TopPriority
        {
            get { return _topPrioritySpeaker; }
        }

        /// <summary>
        /// Get the set of tokens the local player has knowledge of
        /// </summary>
        [NotNull] public IEnumerable<string> Tokens
        {
            get { return _tokens; }
        }

        /// <summary>
        /// Event invoked whenever a new token is added to the local set
        /// </summary>
        public event Action<string> TokenAdded
        {
            add { _tokens.TokenAdded += value; }
            remove { _tokens.TokenAdded += value; }
        }

        /// <summary>
        /// Event invoked whenever a new token is removed from the local set
        /// </summary>
        public event Action<string> TokenRemoved
        {
            add { _tokens.TokenRemoved += value; }
            remove { _tokens.TokenRemoved += value; }
        }
        #endregion

        private VoicePlayback CreatePlayback()
        {
             //The game object must be inactive when it's added to the scene (so it can be edited before it activates)
             PlaybackPrefab.gameObject.SetActive(false);
 
             //Create an instance (currently inactive)
             var entity = Instantiate(PlaybackPrefab.gameObject);
 
             //Configure (and add, if necessary) audio source
             var audioSource = entity.GetComponent<AudioSource>();
             if (audioSource == null)
             {
                 audioSource = entity.AddComponent<AudioSource>();
                 audioSource.rolloffMode = AudioRolloffMode.Linear;
                 audioSource.bypassReverbZones = true;
             }
             audioSource.loop = true;
             audioSource.pitch = 1;
             audioSource.clip = null;
             audioSource.playOnAwake = false;
             audioSource.ignoreListenerPause = true;
             audioSource.spatialBlend = 1;
             audioSource.Stop();
 
             //Configure (and add, if necessary) sample player
             //Because the audio source has no clip, this filter will be "played" instead
             var player = entity.GetComponent<SamplePlaybackComponent>();
             if (player == null)
                 entity.AddComponent<SamplePlaybackComponent>();
 
             //Configure VoicePlayback component
             var playback = entity.GetComponent<VoicePlayback>();
             playback.SetFormat(new WaveFormat(1, _decoderSampleRate), _decoderFrameSize);
             playback.PriorityManager = this;
 
             return playback;
        }

        private void Start()
        {
            //Ensure that all settings are loaded before we access them (potentially from other threads)
            DebugSettings.Preload();
            VoiceSettings.Preload();

            //Write multithreaded logs ASAP so the logging system knows which is the main thread
            Logs.WriteMultithreadedLogs();

            var net = gameObject.GetComponent<ICommsNetwork>();
            if (net == null)
                throw new Exception("Cannot find a voice network component. Please attach a voice network component appropriate to your network system to the DissonanceVoiceComms' entity.");

            if (PlaybackPrefab == null)
            {
                Log.Info("Loading default playback prefab");
                PlaybackPrefab = Resources.Load<GameObject>("PlaybackPrefab").GetComponent<VoicePlayback>();
            }

            net.PlayerJoined += Net_PlayerJoined;
            net.PlayerLeft += Net_PlayerLeft;
            net.VoicePacketReceived += Net_VoicePacketReceived;
            net.PlayerStartedSpeaking += Net_PlayerStartedSpeaking;
            net.PlayerStoppedSpeaking += Net_PlayerStoppedSpeaking;
            net.TextPacketReceived += _text.OnMessageReceived;

            if (string.IsNullOrEmpty(LocalPlayerName))
            {
                var guid = Guid.NewGuid().ToString();
                LocalPlayerName = guid;
            }

            //mark this component as started, locking the LocalPlayerName, PlaybackPrefab and Microphone properties from changing
            _started = true;

            MicCapture = MicrophoneCapture.Start(_micName);

            _localPlayerState = new LocalVoicePlayerState(LocalPlayerName, this);
            _players.Add(_localPlayerState);
            _playersLookup.Add(LocalPlayerName, _localPlayerState);
            
            Action<NetworkMode> networkModeChanged = mode =>
            {
                if (mode.IsClientEnabled())
                {
                    var encoder = new OpusEncoder(VoiceSettings.Instance.Quality, VoiceSettings.Instance.FrameSize);
                    _decoderFrameSize = (uint) encoder.FrameSize;
                    _decoderSampleRate = encoder.SampleRate;

                    _transmissionPipeline = new EncoderPipeline(MicCapture, encoder, _net, () => _playerChannels.Count + _roomChannels.Count);

                    for (var i = 0; i < _activationListeners.Count; i++)
                        MicCapture.Subscribe(_activationListeners[i]);
                }
                else
                {
                    if (_transmissionPipeline != null)
                    {
                        _transmissionPipeline.Dispose();
                        _transmissionPipeline = null;
                    }

                    for (var i = 0; i < _activationListeners.Count; i++)
                        MicCapture.Unsubscribe(_activationListeners[i]);
                }
            };

            if (MicCapture != null)
                net.ModeChanged += networkModeChanged;
            else
                Log.Warn("No microphone detected; local voice transmission will be disabled.");
            
            net.Initialize(LocalPlayerName, Rooms, PlayerChannels, RoomChannels);
            _net = net;
        }

        #region local events
        private void OnChannelOpenedOrClosed(string channel, ChannelProperties properties)
        {
            var channels = _playerChannels.Count + _roomChannels.Count;

            if (channels == 1)
            {
                Log.Debug("Local player started speaking");
                _localPlayerState.InvokeOnStartedSpeaking();
            }
            else if (channels == 0)
            {
                Log.Debug("Local player stopped speaking");
                _localPlayerState.InvokeOnStoppedSpeaking();
            }
        }
        #endregion

        #region network events
        private void Net_PlayerStoppedSpeaking(string player)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (Justification: Sanity check against network system returning incorrect values)
            if (player == null)
            {
                Log.Warn(Log.PossibleBugMessage("Received a player-stopped-speaking event for a null player ID", "5A424BF0-D384-4A63-B6E2-042A1F31A085"));
                return;
            }

            VoicePlayerState state;
            if (_playersLookup.TryGetValue(player, out state))
            {
                state.InvokeOnStoppedSpeaking();

                if (OnPlayerStoppedSpeaking != null)
                    OnPlayerStoppedSpeaking(state);
            }
        }

        private void Net_PlayerStartedSpeaking(string player)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (Justification: Sanity check against network system returning incorrect values)
            if (player == null)
            {
                Log.Warn(Log.PossibleBugMessage("Received a player-started-speaking event for a null player ID", "CA95E783-CA35-441B-9B8B-FAA0FA0B41E3"));
                return;
            }

            VoicePlayerState state;
            if (_playersLookup.TryGetValue(player, out state))
            {
                state.InvokeOnStartedSpeaking();

                if (OnPlayerStartedSpeaking != null)
                    OnPlayerStartedSpeaking(state);
            }
        }

        private void Net_VoicePacketReceived(VoicePacket packet)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (Justification: Sanity check against network system returning incorrect values)
            if (packet.SenderPlayerId == null)
            {
                Log.Warn(Log.PossibleBugMessage("Received a voice packet with a null player ID (discarding)", "C0FE4E98-3CC9-466E-AA39-51F0B6D22D09"));
                return;
            }

            VoicePlayerState state;
            if (_playersLookup.TryGetValue(packet.SenderPlayerId, out state) && state.Playback != null)
                state.Playback.ReceiveAudioPacket(packet);
        }

        private void Net_PlayerLeft(string playerId)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (Justification: Sanity check against network system returning incorrect values)
            if (playerId == null)
            {
                Log.Warn(Log.PossibleBugMessage("Received a player-left event for a null player ID", "37A2506B-6489-4679-BD72-1C53D69797B1"));
                return;
            }

            var state = FindPlayer(playerId);
            if (state != null)
            {
                var playback = state.Playback;
                if (playback != null)
                {
                    playback.gameObject.SetActive(false);
                    playback.PlayerName = null;
                    _playbackPool.Put(playback);
                }

                _playersLookup.Remove(playerId);

                for (var i = _players.Count - 1; i >= 0; i--)
                {
                    if (_players[i].Name == playerId)
                        _players.RemoveAt(i);
                }

                var tracker = state.Tracker;
                if (tracker != null)
                {
                    _unlinkedPlayerTrackers.Add(tracker.PlayerId, tracker);
                    state.Tracker = null;
                }

                state.InvokeOnLeftSession();
                if (OnPlayerLeftSession != null)
                    OnPlayerLeftSession(state);
            }
        }

        private void Net_PlayerJoined(string playerId)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (Justification: Sanity check against network system returning incorrect values)
            if (playerId == null)
            {
                Log.Warn(Log.PossibleBugMessage("Received a player-joined event for a null player ID", "86074592-4BAD-4DF5-9B2C-1DF42A68FAF8"));
                return;
            }

            if (playerId == LocalPlayerName)
                return;

            //Get a playback component for this player
            var playback = _playbackPool.Get();
            playback.transform.parent = transform;
            playback.gameObject.name = "Player " + playerId + " voice comms";
            playback.PlayerName = playerId;

            //Create the state object for this player
            var state = new RemoteVoicePlayerState(playback);
            _players.Add(state);
            _playersLookup.Add(playerId, state);

            //Associate it with the position tracker for this player (if there is one)
            IDissonancePlayer tracker;
            if (_unlinkedPlayerTrackers.TryGetValue(state.Name, out tracker))
            {
                state.Tracker = tracker;
                _unlinkedPlayerTrackers.Remove(state.Name);

                Log.Debug("Linked an unlinked player tracker for player '{0}'", state.Name);
            }

            playback.gameObject.SetActive(true);

            if (OnPlayerJoinedSession != null)
                OnPlayerJoinedSession(state);
        }
        #endregion

        /// <summary>
        /// Find the player state for a given player ID (or null, if it cannot be found)
        /// </summary>
        /// <param name="playerId"></param>
        /// <returns></returns>
        [CanBeNull] public VoicePlayerState FindPlayer(string playerId)
        {
            if (playerId == null)
                throw new ArgumentNullException("playerId");

            VoicePlayerState value;
            if (_playersLookup.TryGetValue(playerId, out value))
                return value;

            return null;
        }

        private void Update()
        {
            Logs.WriteMultithreadedLogs();

            SyncPlaybackPriority();

            for (var i = 0; i < _players.Count; i++)
                Players[i].Update();

            if (MicCapture != null)
                MicCapture.Update();

            if (_transmissionPipeline != null)
                _transmissionPipeline.Update(IsMuted);
        }

        /// <summary>
        /// Determine what the top priority speaker currently is and publish this priority
        /// </summary>
        private void SyncPlaybackPriority()
        {
            var topPriority = ChannelPriority.None;
            string topSpeaker = null;

            //Run through all the current players and find which currently speaking player has the highest priority
            for (var i = 0; i < _players.Count; i++)
            {
                var item = _players[i].Playback;

                if (item == null || !item.IsSpeaking)
                    continue;

                if (item.Priority > topPriority)
                {
                    topPriority = item.Priority;
                    topSpeaker = item.PlayerName;
                }
            }

            if (_topPrioritySpeaker != topPriority)
            {
                _topPrioritySpeaker = topPriority;
                Log.Trace("Highest speaker priority is: {0} ({1})", topPriority, topSpeaker);
            }
        }

        private void OnDestroy()
        {
            if (MicCapture != null)
            {
                MicCapture.Dispose();
                MicCapture = null;
            }

            if (_transmissionPipeline != null)
            {
                _transmissionPipeline.Dispose();
                _transmissionPipeline = null;
            }
        }

        #region VAD
        /// <summary>
        ///     Subscribes to automatic voice detection.
        /// </summary>
        /// <param name="listener">
        ///     The listener which is to receive notification when the player starts and stops speaking via
        ///     automatic voice detection.
        /// </param>
        public void SubcribeToVoiceActivation(IVoiceActivationListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener", "Cannot subscribe with a null listener");

            if (MicCapture == null)
                _activationListeners.Add(listener);
            else
                MicCapture.Subscribe(listener);
        }

        /// <summary>
        ///     Unsubsribes from automatic voice detection.
        /// </summary>
        /// <param name="listener"></param>
        public void UnsubscribeFromVoiceActivation(IVoiceActivationListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener", "Cannot unsubscribe with a null listener");

            _activationListeners.Remove(listener);

            if (MicCapture != null)
                MicCapture.Unsubscribe(listener);
        }
        #endregion

        #region player tracking
        /// <summary>
        /// Enable position tracking for the player represented by the given object
        /// </summary>
        /// <param name="player"></param>
        public void TrackPlayerPosition(IDissonancePlayer player)
        {
            if (player == null)
                throw new ArgumentNullException("player", "Cannot track a null player");

            //Associate tracker with player state
            var state = FindPlayer(player.PlayerId);
            if (state != null)
            {
                state.Tracker = player;
                Log.Debug("Associated position tracking for '{0}'", player.PlayerId);
            }
            else
            {
                _unlinkedPlayerTrackers.Add(player.PlayerId, player);
                Log.Debug("Got a player tracker for player '{0}' but that player doesn't exist yet", player.PlayerId);
            }
        }

        /// <summary>
        /// Stop position tracking for the player represented by the given object
        /// </summary>
        /// <param name="player"></param>
        public void StopTracking(IDissonancePlayer player)
        {
            if (player == null)
                throw new ArgumentNullException("player", "Cannot stop tracking a null player");

            //Try to remove the player from the list of untracked players, just in case we haven't linked it up yet
            if (_unlinkedPlayerTrackers.Remove(player.PlayerId))
            {
                Log.Debug("Removed unlinked state tracker for '{0}' (because StopTracking called)", player.PlayerId);
            }

            //Disassociate the tracker from the player state
            var state = FindPlayer(player.PlayerId);
            if (state != null)
            {
                state.Tracker = null;
                Log.Debug("Disassociated position tracking for '{0}' (because StopTracking called)", player.PlayerId);
            }
        }
        #endregion

        private void OnLocalPlayerNameChanged(string obj)
        {
            var handler = LocalPlayerNameChanged;
            if (handler != null) handler(obj);
        }

        #region tokens
        /// <summary>
        /// Add the given token to the local player
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool AddToken(string token)
        {
            if (token == null)
                throw new ArgumentNullException("token", "Cannot add a null token");

            return _tokens.AddToken(token);
        }

        /// <summary>
        /// Removed the given token from the local player
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool RemoveToken(string token)
        {
            if (token == null)
                throw new ArgumentNullException("token", "Cannot remove a null token");

            return _tokens.RemoveToken(token);
        }

        /// <summary>
        /// Test if the local player knows the given token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool ContainsToken(string token)
        {
            if (token == null)
                throw new ArgumentNullException("token", "Cannot search for a null token");

            return _tokens.ContainsToken(token);
        }

        /// <summary>
        /// Tests if the local player knows has knowledge of *any* of the tokens in the given set
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public bool HasAnyToken(TokenSet tokens)
        {
            if (tokens == null)
                throw new ArgumentNullException("tokens", "Cannot intersect with a null set");

            return _tokens.IntersectsWith(tokens);
        }
        #endregion
    }
}
