using System;

namespace Dissonance.Audio.Playback
{
    internal struct DesyncCalculator
    {
        private static readonly TimeSpan MaxAllowedDesync = TimeSpan.FromMilliseconds(500);

        private const float MaximumPlaybackAdjustment = 0.1f;

        internal int DesyncMilliseconds { get; private set; }

        public float CorrectedPlaybackSpeed
        {
            get { return CalculateCorrectionFactor(DesyncMilliseconds); }
        }

        public TimeSpan Desync
        {
            get { return TimeSpan.FromMilliseconds(DesyncMilliseconds); }
        }

        internal void Update(TimeSpan ideal, TimeSpan actual)
        {
            DesyncMilliseconds = CalculateDesync(ideal, actual);
        }

        internal void Skip(int deltaDesync)
        {
            DesyncMilliseconds += deltaDesync;
        }

        #region static helpers
        private static int CalculateDesync(TimeSpan idealPlaybackPosition, TimeSpan actualPlaybackPosition)
        {
            var desync = idealPlaybackPosition - actualPlaybackPosition;

            // allow for jitter on the output, of the unity audio thread tick rate (20ms)
            const int allowedError = 29;

            double adjustedDesync = 0;
            if (desync.TotalMilliseconds > allowedError)
                adjustedDesync = desync.TotalMilliseconds - allowedError;
            if (desync.TotalMilliseconds < -allowedError)
                adjustedDesync = desync.TotalMilliseconds + allowedError;

            return (int)adjustedDesync;
        }

        private static float CalculateCorrectionFactor(long desyncMilliseconds)
        {
            var alpha = Math.Min(1, Math.Max(desyncMilliseconds / MaxAllowedDesync.TotalMilliseconds, -1));
            return 1 + MaximumPlaybackAdjustment * (float)alpha;
        }
        #endregion
    }
}
