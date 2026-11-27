namespace Stickman.Core
{
    /// <summary>Pure helpers for reading player command-line flags such as <c>-autoshot &lt;folder&gt;</c>.</summary>
    public static class CommandLine
    {
        /// <summary>True if <paramref name="flag"/> appears in <paramref name="args"/> (case-insensitive).</summary>
        public static bool Has(string[] args, string flag)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// The value after <paramref name="flag"/>, or null when the flag is missing or is followed by another flag.
        /// </summary>
        public static string Value(string[] args, string flag)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase)) continue;
                string next = args[i + 1];
                return string.IsNullOrEmpty(next) || next.StartsWith("-") ? null : next;
            }
            return null;
        }
    }
}
