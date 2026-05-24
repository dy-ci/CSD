using System.Collections.Generic;

namespace CSD.Helpers;

internal static class AppSettingsExtensions
{
    public static bool GetBool(this IDictionary<string, object> settings, string key, bool defaultValue = false)
    {
        if (settings.TryGetValue(key, out var value) && value is bool b)
            return b;
        return defaultValue;
    }

    public static int GetInt(this IDictionary<string, object> settings, string key, int defaultValue = 0)
    {
        if (settings.TryGetValue(key, out var value))
        {
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is long l) return (int)l;
        }
        return defaultValue;
    }

    public static string GetString(this IDictionary<string, object> settings, string key, string defaultValue = "")
    {
        if (settings.TryGetValue(key, out var value) && value is string s)
            return s;
        return defaultValue;
    }
}
