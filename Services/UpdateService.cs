using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;




namespace CSD.Services
{
    public sealed class UpdateService
    {
        private const string UpdateCheckUrl = "https://dev-api.dy.ci/api/distribute/check/csd/";

        private static readonly string DeviceId = GetOrCreateDeviceId();

        private static string GetOrCreateDeviceId()
        {
            try
            {
                var settings = AppSettings.Values;
                if (settings.TryGetValue("DeviceId", out var existingId) && existingId is string existingIdStr && !string.IsNullOrEmpty(existingIdStr))
                {
                    return existingIdStr;
                }

                var newId = Guid.NewGuid().ToString("N");
                settings["DeviceId"] = newId;
                return newId;
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        public static string GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version?.Major}.{version?.Minor}.{version?.Build}";
            }
            catch
            {
                return "0.0.0";
            }
        }

        public static int GetCurrentVersionCode()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version!.Major * 10000 + version.Minor * 100 + version.Build;
            }
            catch
            {
                return 10000;
            }
        }

        public static (string Os, string Arch) GetSystemInfo()
        {
            var os = Environment.OSVersion.Version.Major >= 10 ? "win10" : "win11";
            var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            return (os, arch);
        }

        public static string GetDeviceId() => DeviceId;

        public static string GetUpdateChannel()
        {
            try
            {
                var channel = AppSettings.Values["UpdateChannel"] as string;
                return string.IsNullOrWhiteSpace(channel) ? "stable" : channel;
            }
            catch
            {
                return "stable";
            }
        }

        public static void SetUpdateChannel(string channel)
        {
            AppSettings.Values["UpdateChannel"] = channel;
        }

        public static string GetUpdateCheckMode()
        {
            try
            {
                var mode = AppSettings.Values["UpdateCheckMode"] as string;
                return string.IsNullOrWhiteSpace(mode) ? "startup" : mode;
            }
            catch
            {
                return "startup";
            }
        }

        public static void SetUpdateCheckMode(string mode)
        {
            AppSettings.Values["UpdateCheckMode"] = mode;
        }

        public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var (os, arch) = GetSystemInfo();
                var channel = GetUpdateChannel();
                var url = $"{UpdateCheckUrl}?os={os}&arch={arch}&channel={channel}&user_id={Uri.EscapeDataString(DeviceId)}";

                using var response = await AppHttpClient.Instance.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var updateInfo = new UpdateInfo
                {
                    HasUpdate = root.TryGetProperty("has_update", out var hasUpdate) && hasUpdate.GetBoolean(),
                    Version = root.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "",
                    VersionCode = root.TryGetProperty("version_code", out var versionCode) ? versionCode.GetInt32() : 0,
                    Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                    ReleaseNotes = root.TryGetProperty("release_notes", out var releaseNotes) ? releaseNotes.GetString() ?? "" : "",
                    FileSize = root.TryGetProperty("file_size", out var fileSize) ? fileSize.GetInt64() : 0,
                    DownloadUrl = root.TryGetProperty("download_url", out var downloadUrl) ? downloadUrl.GetString() ?? "" : "",
                    TargetOs = root.TryGetProperty("target_os", out var targetOs) ? targetOs.GetString() ?? "" : "",
                    TargetArch = root.TryGetProperty("target_arch", out var targetArch) ? targetArch.GetString() ?? "" : "",
                    ReleaseDate = root.TryGetProperty("release_date", out var releaseDate)
                        ? DateTime.TryParse(releaseDate.GetString(), out var date) ? date : DateTime.MinValue
                        : DateTime.MinValue,
                    Sha256 = root.TryGetProperty("sha256", out var sha256) ? sha256.GetString() : null
                };

                if (!updateInfo.HasUpdate)
                {
                    return updateInfo;
                }

                if (updateInfo.VersionCode <= GetCurrentVersionCode())
                {
                    updateInfo.HasUpdate = false;
                }

                return updateInfo;
            }
            catch
            {
                return null;
            }
        }
    }
}
