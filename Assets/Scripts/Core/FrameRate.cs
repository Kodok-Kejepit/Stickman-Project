namespace Stickman.Core
{
    /// <summary>Pure frame-rate policy: which target FPS to request from the engine.</summary>
    public static class FrameRate
    {
        public const int Fallback = 60;
        public const int Min = 30;
        public const int Max = 120;

        /// <summary>
        /// Picks the target frame rate. <paramref name="preferred"/> &gt; 0 is the player's choice (60/90/120),
        /// capped by the display refresh rate; otherwise the display refresh rate is used.
        /// A missing or invalid refresh rate falls back to 60. The result is always within [30, 120].
        /// </summary>
        public static int ChooseTarget(double refreshRateHz, int preferred)
        {
            int display = refreshRateHz > 1.0 ? (int)System.Math.Round(refreshRateHz) : Fallback;
            int target = preferred > 0 ? System.Math.Min(preferred, display) : display;
            if (target < Min) return Min;
            if (target > Max) return Max;
            return target;
        }
    }
}
