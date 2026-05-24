using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Models
{
    internal static class AppSettings
    {
        private static readonly SettingsDictionary Settings = new();

        public static IDictionary<string, object> Values => Settings;

        /// <summary>
        /// 获取应用资源文件的 URI（兼容打包和非打包部署）。
        /// 优先使用 file:// 协议从 exe 目录加载，回退到 ms-appx:// 协议。
        /// </summary>
        public static Uri GetAssetUri(string relativePath)
        {
            // Implementation for "High-Res Resource Loading" setting
            var isHighResEnabled = Values.GetBool("Settings_HighResResourceLoading", true);
            if (!isHighResEnabled)
            {
                // In a real scenario, we might look for a .lowres version or smaller file
                // For now, we just log or provide a hook for future optimized assets
            }

            var exeDir = AppContext.BaseDirectory;
            var fullPath = Path.Combine(exeDir, relativePath);
            if (File.Exists(fullPath))
            {
                return new Uri("file:///" + fullPath.Replace('\\', '/'));
            }
            return new Uri("ms-appx:///" + relativePath);
        }
    }

    /// <summary>与 Classworks KV 服务端约定的特殊键名（用于 GET/POST /kv/&lt;键名&gt;）。</summary>
    internal static class ClassworksKvKeys
    {
        public const string SubjectConfig = "classworks-config-subject";

        /// <summary>学生名单主数据，和云端名单管理共用同一个 KV 键名。</summary>
        public const string RosterConfig = "classworks-list-main";

        /// <summary>编辑偏好配置键名。</summary>
        public const string EditConfig = "classworks-config-edit";
    }

    internal sealed class SettingsDictionary : IDictionary<string, object>
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _settingsFilePath;
        private readonly Dictionary<string, object> _settings;
        private readonly object _syncRoot = new();

        public SettingsDictionary()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSD");

            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            _settings = LoadSettings();
        }

        public object this[string key]
        {
            get
            {
                lock (_syncRoot)
                {
                    return _settings.TryGetValue(key, out var value) ? value : null!;
                }
            }
            set
            {
                lock (_syncRoot)
                {
                    _settings[key] = value;
                    SaveSettings();
                }
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                lock (_syncRoot)
                {
                    return new List<string>(_settings.Keys);
                }
            }
        }

        public ICollection<object> Values
        {
            get
            {
                lock (_syncRoot)
                {
                    return new List<object>(_settings.Values);
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _settings.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(string key, object value)
        {
            lock (_syncRoot)
            {
                _settings.Add(key, value);
                SaveSettings();
            }
        }

        public bool ContainsKey(string key)
        {
            lock (_syncRoot)
            {
                return _settings.ContainsKey(key);
            }
        }

        public bool Remove(string key)
        {
            lock (_syncRoot)
            {
                var removed = _settings.Remove(key);
                if (removed)
                {
                    SaveSettings();
                }

                return removed;
            }
        }

        public bool TryGetValue(string key, out object value)
        {
            lock (_syncRoot)
            {
                if (_settings.TryGetValue(key, out var existingValue))
                {
                    value = existingValue;
                    return true;
                }

                value = null!;
                return false;
            }
        }

        public void Add(KeyValuePair<string, object> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _settings.Clear();
                SaveSettings();
            }
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            lock (_syncRoot)
            {
                return ((ICollection<KeyValuePair<string, object>>)_settings).Contains(item);
            }
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            lock (_syncRoot)
            {
                ((ICollection<KeyValuePair<string, object>>)_settings).CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            lock (_syncRoot)
            {
                var removed = ((ICollection<KeyValuePair<string, object>>)_settings).Remove(item);
                if (removed)
                {
                    SaveSettings();
                }

                return removed;
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            lock (_syncRoot)
            {
                return new List<KeyValuePair<string, object>>(_settings).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private Dictionary<string, object> LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<string, object>(StringComparer.Ordinal);
                }

                var rawData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (rawData is null)
                {
                    return new Dictionary<string, object>(StringComparer.Ordinal);
                }

                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var item in rawData)
                {
                    result[item.Key] = ConvertJsonElement(item.Value);
                }

                return result;
            }
            catch
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, SerializerOptions);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so the app can continue running.
            }
        }

        private static object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => element.GetRawText()
            };
        }
    }
}
