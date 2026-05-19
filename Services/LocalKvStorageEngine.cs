using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSD.Models;

namespace CSD.Services
{
    /// <summary>
    /// 本地存储引擎，用于模拟云端 KV 存储在本地环境的读写操作。
    /// 支持存储空间溢出模拟、隐私模式（内存模式）等边界场景。
    /// </summary>
    public static class LocalKvStorageEngine
    {
        private const long MaxLocalStorageSize = 50 * 1024 * 1024; // 模拟 50MB 存储上限
        
        // 隐私模式开关（启用后仅在内存中存取，不落盘）
        public static bool IsPrivacyMode { get; set; } = false;
        private static readonly Dictionary<string, string> _privacyModeStorage = new();

        public static string LocalStorageDirectory
        {
            get
            {
                return Path.Combine(AppContext.BaseDirectory, "LocalStorage");
            }
        }

        /// <summary>
        /// 拦截请求并路由到本地文件/内存读取
        /// </summary>
        public static async Task<HttpResponseMessage> HandleRequestAsync(HttpMethod method, string path, string? jsonBody)
        {
            var key = path.StartsWith("/kv/") ? path.Substring(4) : path.TrimStart('/');
            var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            
            try
            {
                if (method == HttpMethod.Get)
                {
                    string? content = await ReadLocalAsync(safeKey);
                    if (content == null)
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content, Encoding.UTF8, "application/json")
                    };
                }
                else if (method == HttpMethod.Post || method == HttpMethod.Put)
                {
                    await WriteLocalAsync(safeKey, jsonBody ?? "{}");
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                }
                else if (method == HttpMethod.Delete)
                {
                    await DeleteLocalAsync(safeKey);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                }
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        public static async Task<string?> ReadLocalAsync(string key)
        {
            if (IsPrivacyMode)
            {
                if (_privacyModeStorage.TryGetValue(key, out var value))
                    return value;
                var fallback = GetFallbackDataForKey(key);
                if (fallback != null)
                {
                    _privacyModeStorage[key] = fallback;
                }
                return fallback;
            }

            var filePath = Path.Combine(LocalStorageDirectory, $"{key}.json");
            if (!File.Exists(filePath))
            {
                var fallback = GetFallbackDataForKey(key);
                if (fallback != null)
                {
                    await WriteLocalAsync(key, fallback);
                }
                return fallback;
            }

            return await File.ReadAllTextAsync(filePath);
        }

        private static string? GetFallbackDataForKey(string key)
        {
            if (key == "classworks-config-subject")
            {
                var raw = AppSettings.Values["Settings_SubjectList"] as string;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw);
                        if (list != null)
                        {
                            var payload = new List<Dictionary<string, object>>();
                            for (var i = 0; i < list.Count; i++)
                                payload.Add(new Dictionary<string, object> { ["order"] = i + 1, ["name"] = list[i] });
                            return System.Text.Json.JsonSerializer.Serialize(payload);
                        }
                    }
                    catch { }
                }
                
                // 默认科目作为最终兜底
                var defaultSubjects = new string[] { "语文", "数学", "英语", "物理", "化学", "生物", "政治", "历史", "地理", "其他" };
                var defPayload = new List<Dictionary<string, object>>();
                for (var i = 0; i < defaultSubjects.Length; i++)
                    defPayload.Add(new Dictionary<string, object> { ["order"] = i + 1, ["name"] = defaultSubjects[i] });
                return System.Text.Json.JsonSerializer.Serialize(defPayload);
            }
            else if (key == "classworks-list-main")
            {
                var raw = AppSettings.Values["Settings_RosterList"] as string;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw);
                        if (list != null)
                        {
                            var payload = new List<Dictionary<string, object>>();
                            for (var i = 0; i < list.Count; i++)
                                payload.Add(new Dictionary<string, object> { ["order"] = i + 1, ["name"] = list[i] });
                            return System.Text.Json.JsonSerializer.Serialize(payload);
                        }
                    }
                    catch { }
                }
                return "[]";
            }

            return null;
        }

        public static async Task WriteLocalAsync(string key, string data)
        {
            if (IsPrivacyMode)
            {
                // 模拟内存溢出
                long memSize = 0;
                foreach (var v in _privacyModeStorage.Values) memSize += v.Length;
                if (memSize + data.Length > MaxLocalStorageSize)
                {
                    throw new InvalidOperationException("QuotaExceededError: Local storage space exceeded limit in privacy mode.");
                }
                _privacyModeStorage[key] = data;
                return;
            }

            if (!Directory.Exists(LocalStorageDirectory))
            {
                Directory.CreateDirectory(LocalStorageDirectory);
            }

            long currentSize = GetDirectorySize(LocalStorageDirectory);
            if (currentSize + data.Length > MaxLocalStorageSize)
            {
                throw new InvalidOperationException("QuotaExceededError: Local storage space exceeded limit.");
            }

            var filePath = Path.Combine(LocalStorageDirectory, $"{key}.json");
            await File.WriteAllTextAsync(filePath, data);
        }

        public static Task DeleteLocalAsync(string key)
        {
            if (IsPrivacyMode)
            {
                _privacyModeStorage.Remove(key);
                return Task.CompletedTask;
            }

            var filePath = Path.Combine(LocalStorageDirectory, $"{key}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }

        private static long GetDirectorySize(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return 0;
            var dirInfo = new DirectoryInfo(folderPath);
            long size = 0;
            foreach (var fi in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                size += fi.Length;
            }
            return size;
        }
        
        public static void ClearAll()
        {
            _privacyModeStorage.Clear();
            if (Directory.Exists(LocalStorageDirectory))
            {
                Directory.Delete(LocalStorageDirectory, true);
            }
        }
    }
}
