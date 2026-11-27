using System;
using UnityEngine;

namespace Stickman.Core
{
    /// <summary>Loads, saves, and serializes <see cref="SettingsData"/> to JSON / PlayerPrefs.</summary>
    public static class SettingsStore
    {
        public const string PrefsKey = "StickmanSettingsV1";

        static SettingsData _cached;

        public static SettingsData Current
        {
            get
            {
                if (_cached == null) _cached = Load();
                return _cached;
            }
        }

        public static string ToJson(SettingsData data)
        {
            var d = data ?? SettingsData.Default();
            d.Clamp();
            return JsonUtility.ToJson(d, false);
        }

        public static SettingsData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return SettingsData.Default();
            try
            {
                var parsed = JsonUtility.FromJson<SettingsData>(json);
                if (parsed == null) return SettingsData.Default();
                parsed.Clamp();
                return parsed;
            }
            catch (Exception)
            {
                return SettingsData.Default();
            }
        }

        public static SettingsData Load()
        {
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            _cached = FromJson(raw);
            return _cached;
        }

        public static void Save(SettingsData data)
        {
            var d = data ?? SettingsData.Default();
            d.Clamp();
            _cached = d;
            PlayerPrefs.SetString(PrefsKey, ToJson(d));
            PlayerPrefs.Save();
        }

        public static SettingsData ResetToDefaults()
        {
            var d = SettingsData.Default();
            Save(d);
            return d;
        }
    }
}
