using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Dissonance.Config;
using NAudio.Wave;

namespace Dissonance.Audio.Capture
{
    internal class WebRtcPreprocessingPipeline
        : BasePreprocessingPipeline
    {
        #region fields and properties
        private static readonly Log Log = Logs.Create(LogCategory.Recording, typeof(WebRtcPreprocessingPipeline).Name);

        /// <summary>
        /// This pipeline always requires input because it's always running AEC and VAD
        /// </summary>
        public override bool RequiresInput
        {
            get { return true; }
        }

        protected override bool VadIsSpeechDetected
        {
            get { return _preprocessor.IsVadDetectingSpeech; }
        }

        private readonly WebRtcPreprocessor _preprocessor;
        #endregion

        #region construction
        public WebRtcPreprocessingPipeline(WaveFormat inputFormat)
            : base(inputFormat, CalculateInputFrameSize(inputFormat.SampleRate), 480, 48000, 480, 48000)
        {
            _preprocessor = new WebRtcPreprocessor(NoiseSuppressionLevels.High);
        }

        private static int CalculateInputFrameSize(int inputSampleRate)
        {
            //Take input in 20ms frames
            return (int)(inputSampleRate * 0.02);
        }
        #endregion

        public override void Dispose()
        {
            _preprocessor.Dispose();

            base.Dispose();
        }

        protected override void ApplyReset()
        {
            _preprocessor.Reset();

            base.ApplyReset();
        }

        protected override void PreprocessAudioFrame(float[] frame)
        {
            _preprocessor.Process(WebRtcPreprocessor.SampleRates.SampleRate48KHz, frame, frame);

            SendSamplesToSubscribers(frame);
        }

        private sealed class WebRtcPreprocessor
            : IDisposable
        {
            #region native methods
#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern IntPtr Dissonance_CreatePreprocessor(NoiseSuppressionLevels nsLevel);

#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern void Dissonance_DestroyPreprocessor(IntPtr handle);

#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern void Dissonance_ConfigureNoiseSuppression(IntPtr handle, NoiseSuppressionLevels nsLevel);

#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern bool Dissonance_GetVadSpeechState(IntPtr handle);

#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern ProcessorErrors Dissonance_PreprocessCaptureFrame(IntPtr handle, int sampleRate, float[] input, float[] output);

#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern bool Dissonance_PreprocessorExchangeInstance(IntPtr previous, IntPtr replacement);

#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")]
#else
            [DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
#endif
            private static extern int Dissonance_GetFilterState();

            public enum SampleRates
            {
                // ReSharper disable UnusedMember.Local
                SampleRate8KHz = 8000,
                SampleRate16KHz = 16000,
                SampleRate32KHz = 32000,
                SampleRate48KHz = 48000,
                // ReSharper restore UnusedMember.Local
            }

            private enum ProcessorErrors
            {
                // ReSharper disable UnusedMember.Local
                Ok,

                Unspecified = -1,
                CreationFailed = -2,
                UnsupportedComponent = -3,
                UnsupportedFunction = -4,
                NullPointer = -5,
                BadParameter = -6,
                BadSampleRate = -7,
                BadDataLength = -8,
                BadNumberChannels = -9,
                FileError = -10,
                StreamParameterNotSet = -11,
                NotEnabled = -12,
                // ReSharper restore UnusedMember.Local
            }

            private enum FilterState
            {
                // ReSharper disable UnusedMember.Local
                FilterNotRunning,
                FilterNoInstance,
                FilterNoSamplesSubmitted,
                FilterOk
                // ReSharper restore UnusedMember.Local
            }
            #endregion

            #region properties and fields
            private IntPtr _handle;

            private readonly List<PropertyChangedEventHandler> _subscribed = new List<PropertyChangedEventHandler>();

            private NoiseSuppressionLevels _nsLevel;
            public NoiseSuppressionLevels NoiseSuppressionLevel
            {
                get { return _nsLevel; }
                set
                {
                    _nsLevel = value;
                    if (_handle != IntPtr.Zero)
                        Dissonance_ConfigureNoiseSuppression(_handle, _nsLevel);
                }
            }

            public bool IsVadDetectingSpeech
            {
                get
                {
                    if (_handle == IntPtr.Zero)
                        return false;
                    return Dissonance_GetVadSpeechState(_handle);
                }
            }
            #endregion

            public WebRtcPreprocessor(NoiseSuppressionLevels nsLevel)
            {
                _nsLevel = nsLevel;
                _handle = Dissonance_CreatePreprocessor(nsLevel);

                SetFilterPreprocessor(_handle);
            }

            public void Process(SampleRates inputSampleRate, float[] input, float[] output)
            {
                var result = Dissonance_PreprocessCaptureFrame(_handle, (int)inputSampleRate, input, output);
                if (result != ProcessorErrors.Ok)
                    throw new DissonanceException(Log.PossibleBugMessage(string.Format("Preprocessor error: '{0}'", result), "0A89A5E7-F527-4856-BA01-5A19578C6D88"));
            }

            public void Reset()
            {
                Log.Debug("Resetting");

                if (_handle != IntPtr.Zero)
                {
                    //Clear from playback filter. This internally acquires a lock and will not complete until it is safe to (i.e. no one else is using the preprocessor concurrently).
                    ClearFilterPreprocessor();

                    //Destroy it
                    Dissonance_DestroyPreprocessor(_handle);
                    _handle = IntPtr.Zero;
                }

                //Create a new one
                _handle = Dissonance_CreatePreprocessor(_nsLevel);

                //Associate with playback filter
                SetFilterPreprocessor(_handle);
            }

            private void SetFilterPreprocessor(IntPtr preprocessor)
            {
                Log.Debug("Exchanging preprocessor instance in playback filter...");

                if (!Dissonance_PreprocessorExchangeInstance(IntPtr.Zero, _handle))
                    throw Log.CreatePossibleBugException("Cannot associate preprocessor with Playback filter - one already exists", "D5862DD2-B44E-4605-8D1C-29DD2C72A70C");

                Log.Debug("...Exchanged preprocessor instance in playback filter");

                var state = (FilterState)Dissonance_GetFilterState();
                if (state == FilterState.FilterNotRunning)
                    Log.Warn("Associated preprocessor with playback filter - but filter is not running");

                Bind(s => s.DenoiseAmount, "DenoiseAmount", v => NoiseSuppressionLevel = (NoiseSuppressionLevels)v);
            }

            private void Bind<T>(Func<VoiceSettings, T> getValue, string propertyName, Action<T> setValue)
            {
                var settings = VoiceSettings.Instance;

                //Bind for value changes in the future
                PropertyChangedEventHandler subbed;
                settings.PropertyChanged += subbed = (sender, args) => {
                    if (args.PropertyName == propertyName)
                        setValue(getValue(settings));
                };

                //Save this subscription so we can *unsub* later
                _subscribed.Add(subbed);

                //Invoke immediately to pull the current value
                subbed.Invoke(settings, new PropertyChangedEventArgs(propertyName));
            }

            private bool ClearFilterPreprocessor(bool throwOnError = true)
            {
                Log.Debug("Clearing preprocessor instance in playback filter...");

                //Clear binding in native code
                if (!Dissonance_PreprocessorExchangeInstance(_handle, IntPtr.Zero))
                {
                    if (throwOnError)
                        throw Log.CreatePossibleBugException("Cannot clear preprocessor from Playback filter", "6323106A-04BD-4217-9ECA-6FD49BF04FF0");
                    else
                        Log.Error("Failed to clear preprocessor from playback filter", "CBC6D727-BE07-4073-AA5A-F750A0CC023D");

                    return false;
                }

                //Clear event handlers from voice settings
                var settings = VoiceSettings.Instance;
                for (var i = 0; i < _subscribed.Count; i++)
                    settings.PropertyChanged -= _subscribed[i];
                _subscribed.Clear();

                Log.Debug("...Cleared preprocessor instance in playback filter");
                return true;
            }

            #region dispose
            private void ReleaseUnmanagedResources()
            {
                if (_handle != IntPtr.Zero)
                {
                    ClearFilterPreprocessor(throwOnError: false);

                    Dissonance_DestroyPreprocessor(_handle);
                    _handle = IntPtr.Zero;
                }
            }

            public void Dispose()
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }

            ~WebRtcPreprocessor()
            {
                ReleaseUnmanagedResources();
            }
            #endregion
        }
    }
}
