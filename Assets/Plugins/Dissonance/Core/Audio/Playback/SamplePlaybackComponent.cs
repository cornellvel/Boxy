using System;
using System.Threading;
using Dissonance.Config;
using UnityEngine;

namespace Dissonance.Audio.Playback
{
    /// <summary>
    /// Plays back an ISampleProvider to an AudioSource
    /// <remarks>Uses OnAudioFilterRead, so the source it is playing back on will be whichever the filter attaches itself to.</remarks>
    /// </summary>
    public class SamplePlaybackComponent
        : MonoBehaviour
    {
        #region fields
        private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(SamplePlaybackComponent).Name);
        private static readonly TimeSpan ResetDesync = TimeSpan.FromSeconds(1);

        private AudioSource _audioSource;

        private DesyncCalculator _desync;

        private long _totalSamplesRead;

        /// <summary>
        /// Temporary buffer to hold data read from source
        /// </summary>
        private float[] _temp;

        [CanBeNull]private AudioFileWriter _diagnosticOutput;

        /// <summary>
        /// Configure this playback component to either overwrite the input audio, or to multiply by it
        /// </summary>
        internal bool MultiplyBySource { get; set; }

        public bool HasActiveSession
        {
            get { return Session.HasValue; }
        }

        private SessionContext _lastPlayedSessionContext;
        private readonly ReaderWriterLockSlim _sessionLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        public SpeechSession? Session { get; private set; }

        public TimeSpan PlaybackPosition
        {
            get
            {
                var session = Session;
                if (session == null)
                    return TimeSpan.Zero;

                return TimeSpan.FromSeconds(Interlocked.Read(ref _totalSamplesRead) / (double)session.Value.OutputWaveFormat.SampleRate);
            }
        }
        public TimeSpan IdealPlaybackPosition
        {
            get
            {
                var session = Session;
                if (session == null)
                    return TimeSpan.Zero;

                return DateTime.Now - session.Value.ActivationTime;
            }
        }

        public TimeSpan Desync { get { return _desync.Desync; } }
        public float CorrectedPlaybackSpeed { get { return _desync.CorrectedPlaybackSpeed; } }

        private volatile float _arv;
        /// <summary>
        /// Average rectified value of the audio signal currently playing (a measure of amplitude)
        /// </summary>
        public float ARV { get { return _arv; } }
        #endregion

        public void Play(SpeechSession session)
        {
            if (Session != null)
                throw Log.CreatePossibleBugException("Attempted to play a session when one is already playing", "C4F19272-994D-4025-AAEF-37BB62685C2E");

            Log.Debug("Began playback of speech session. id={0}", session.Context.Id);

            if (DebugSettings.Instance.EnablePlaybackDiagnostics && DebugSettings.Instance.RecordFinalAudio)
            {
                var filename = string.Format("Dissonance_Diagnostics/Output_{0}_{1}_{2}", session.Context.PlayerName, session.Context.Id, DateTime.UtcNow.ToFileTime());
                Interlocked.Exchange(ref _diagnosticOutput, new AudioFileWriter(filename, session.OutputWaveFormat));
            }

            _sessionLock.EnterWriteLock();
            try
            {
                ApplyReset();
                Session = session;
            }
            finally
            {
                _sessionLock.ExitWriteLock();
            }
        }

        public void Start()
        {
            //Create a temporary buffer to hold audio. We don't know how big the buffer needs to be,
            //but this buffer is *one second long* which is way larger than we could ever need!
            _temp = new float[AudioSettings.outputSampleRate];
            _audioSource = GetComponent<AudioSource>();
        }

        public void OnEnable()
        {
            Session = null;
            ApplyReset();
        }

        public void OnDisable()
        {
            Session = null;
            ApplyReset();
        }

        public void Update()
        {
            _audioSource.pitch = _desync.CorrectedPlaybackSpeed;
        }

        public void OnAudioFilterRead(float[] data, int channels)
        {
            //If there is no session, clear filter and early exit
            var maybeSession = Session;
            if (!maybeSession.HasValue)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            _sessionLock.EnterUpgradeableReadLock();
            try
            {
                //Check if there is no session again, this time protected by a lock
                maybeSession = Session;
                if (!maybeSession.HasValue)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                //Detect if the session has changed since the last call to this method, if so reset
                if (!maybeSession.Value.Context.Equals(_lastPlayedSessionContext))
                {
                    _lastPlayedSessionContext = maybeSession.Value.Context;
                    ApplyReset();
                }

                //Calculate the difference between where we should be and where we are (in samples)
                var session = maybeSession.Value;
                _desync.Update(IdealPlaybackPosition, PlaybackPosition);

                //If necessary skip samples to bring us back in sync
                int deltaDesync, deltaSamples;
                var complete = Skip(session, _desync.DesyncMilliseconds, out deltaSamples, out deltaDesync);
                Interlocked.Add(ref _totalSamplesRead, deltaSamples);
                _desync.Skip(deltaDesync);

                //If the session wasn't completed by the skip, keep playing
                if (!complete)
                {
                    int samples;
                    float arv;
                    complete = Filter(session, data, channels, _temp, _diagnosticOutput, out arv, out samples, MultiplyBySource);
                    _arv = arv;
                    Interlocked.Add(ref _totalSamplesRead, samples);
                }

                //Clean up now that this session is complete
                if (complete)
                {
                    Log.Debug("Finished playback of speech session. id={0}", session.Context.Id);

                    //Clear the session
                    _sessionLock.EnterWriteLock();
                    try
                    {
                        Session = null;
                    }
                    finally
                    {
                        _sessionLock.ExitWriteLock();
                    }

                    //Reset the state
                    ApplyReset();

                    //Discard the diagnostic recorder if necessary
                    if (_diagnosticOutput != null)
                    {
                        _diagnosticOutput.Dispose();
                        _diagnosticOutput = null;
                    }
                }
            }
            finally
            {
                _sessionLock.ExitUpgradeableReadLock();
            }
        }

        private void ApplyReset()
        {
            Log.Debug("Resetting playback component");

            Interlocked.Exchange(ref _totalSamplesRead, 0);
            _arv = 0;
            _desync = new DesyncCalculator();
        }

        internal static bool Skip(SpeechSession session, int desyncMilliseconds, out int deltaSamples, out int deltaDesyncMilliseconds)
        {
            //If we're really far out of sync just skip forward the playback
            if (desyncMilliseconds > ResetDesync.TotalMilliseconds)
            {
                Log.Warn("Playback desync ({0}ms) beyond recoverable threshold; resetting stream to current time", desyncMilliseconds);

                deltaSamples = desyncMilliseconds * session.OutputWaveFormat.SampleRate / 1000;
                deltaDesyncMilliseconds = -desyncMilliseconds;

                // skip through the session the required number of samples
                // we allocate here, but we are already in an error case rather than normal operation
                return session.Read(new ArraySegment<float>(new float[deltaSamples]));
            }

            deltaSamples = 0;
            deltaDesyncMilliseconds = 0;
            return false;
        }

        internal static bool Filter(SpeechSession session, float[] data, int channels, float[] temp, [CanBeNull]AudioFileWriter diagnosticOutput, out float arv, out int samplesRead, bool multiply)
        {
            //Read out data from source (exactly as much as we need for one channel)
            var samplesRequired = data.Length / channels;
            var complete = session.Read(new ArraySegment<float>(temp, 0, samplesRequired));

            if (diagnosticOutput != null)
                diagnosticOutput.WriteSamples(new ArraySegment<float>(temp, 0, samplesRequired));

            float accumulator = 0;

            //Step through samples, stretching them (i.e. play mono input in all output channels)
            var sampleIndex = 0;
            for (var i = 0; i < data.Length; i += channels)
            {
                //Get a single sample from the source data
                var sample = temp[sampleIndex++];

                //Accumulate the sum of the audio signal
                accumulator += Mathf.Abs(sample);

                //Copy data into all channels
                for (var c = 0; c < channels; c++)
                {
                    if (multiply)
                        data[i + c] *= sample;
                    else
                        data[i + c] = sample;
                }
            }

            arv = accumulator / data.Length;
            samplesRead = samplesRequired;

            return complete;
        }
    }
}
