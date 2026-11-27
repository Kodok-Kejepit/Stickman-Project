namespace Stickman.Core
{
    /// <summary>
    /// Fixed-capacity recorder of frame times (seconds). Allocates once in the constructor, never per frame.
    /// </summary>
    public sealed class FrameStats
    {
        readonly float[] _samples;
        int _count;

        public FrameStats(int capacity)
        {
            _samples = new float[capacity < 1 ? 1 : capacity];
        }

        public int Count => _count;
        public int Capacity => _samples.Length;

        /// <summary>Adds one frame time. Samples beyond capacity are ignored.</summary>
        public void Add(float seconds)
        {
            if (_count < _samples.Length) _samples[_count++] = seconds;
        }

        public void Clear() => _count = 0;

        /// <summary>Average frames per second over the recorded samples (0 when empty).</summary>
        public double AverageFps()
        {
            if (_count == 0) return 0;
            double total = 0;
            for (int i = 0; i < _count; i++) total += _samples[i];
            return total > 0 ? _count / total : 0;
        }

        /// <summary>
        /// Frame time (milliseconds) at the given percentile (0..100), nearest-rank method. Allocates a sorted copy,
        /// so call it only when reporting, not every frame.
        /// </summary>
        public double PercentileMs(double percentile)
        {
            if (_count == 0) return 0;
            var sorted = new float[_count];
            System.Array.Copy(_samples, sorted, _count);
            System.Array.Sort(sorted);
            if (percentile <= 0) return sorted[0] * 1000.0;
            if (percentile >= 100) return sorted[_count - 1] * 1000.0;
            int rank = (int)System.Math.Ceiling(percentile / 100.0 * _count);
            return sorted[rank - 1] * 1000.0;
        }
    }
}
