using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CSD.Services
{
    public enum NotificationSound
    {
        Default,
        Call,
        Silent
    }

    /// <summary>
    /// 通知服务单例，统一管理所有 AppNotification 的构建与发送。
    /// 在应用启动时调用 <see cref="Initialize"/>，退出时调用 <see cref="Dispose"/>。
    /// </summary>
    public sealed class NotificationService : IDisposable
    {
        private static NotificationService? _instance;
        private bool _disposed;
        private bool _initialized;

        private NotificationService()
        {
        }

        /// <summary>
        /// 获取全局唯一的 NotificationService 实例。
        /// </summary>
        public static NotificationService Instance => _instance ??= new NotificationService();

        /// <summary>
        /// 当用户点击通知中的 "open_window" 按钮时触发，
        /// 订阅方应在 UI 线程上激活主窗口。
        /// </summary>
        public event Action? OpenWindowRequested;

        /// <summary>
        /// 初始化通知服务：注册 COM activator 并挂载通知回调事件。
        /// 应在应用启动时（OnLaunched）调用一次。
        /// </summary>
        public void Initialize()
        {
            if (_initialized || _disposed)
                return;

            _initialized = true;

            try
            {
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;

#pragma warning disable CS0618 // 保留 Start Menu 快捷方式以确保 unpackaged 下 AUMID 完整
                ToastHelper.EnsureAumidRegistration();
#pragma warning restore CS0618

                AppNotificationManager.Default.Register();
                Debug.WriteLine("NotificationService initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.Initialize failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 显示一条仅含标题和正文的简单通知。
        /// </summary>
        /// <param name="title">通知标题。</param>
        /// <param name="body">通知正文。</param>
        /// <param name="sound">音效配置。</param>
        public void ShowSimple(string title, string body, NotificationSound sound = NotificationSound.Default)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body);
                ApplySound(builder, sound);
                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.ShowSimple failed: " + ex.Message);
                FallbackToTray(title, body);
            }
        }

        /// <summary>
        /// 显示带操作按钮的通知。
        /// </summary>
        /// <param name="title">通知标题。</param>
        /// <param name="body">通知正文。</param>
        /// <param name="buttons">1~3 个操作按钮，每个按钮通过 AddArgument 携带 action 参数。</param>
        /// <param name="sound">音效配置。</param>
        public void ShowWithButtons(string title, string body, IReadOnlyList<AppNotificationButton> buttons, NotificationSound sound = NotificationSound.Default)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body);

                foreach (var button in buttons)
                {
                    builder.AddButton(button);
                }

                ApplySound(builder, sound);
                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.ShowWithButtons failed: " + ex.Message);
                FallbackToTray(title, body);
            }
        }

        /// <summary>
        /// 显示包含 "已读" 按钮的通知，点击后自动发送 notification-read 回执。
        /// </summary>
        /// <param name="title">通知标题。</param>
        /// <param name="body">通知正文。</param>
        /// <param name="notificationId">通知 ID，用于回执。</param>
        /// <param name="sound">音效配置。</param>
        public void ShowWithReadButton(string title, string body, string notificationId, NotificationSound sound = NotificationSound.Default)
        {
            var button = new AppNotificationButton("已读")
                .AddArgument("action", "mark_as_read")
                .AddArgument("notificationId", notificationId);

            ShowWithButtons(title, body, [button], sound);
        }

        /// <summary>
        /// 显示带 Hero Image 的通知。
        /// </summary>
        /// <param name="title">通知标题。</param>
        /// <param name="body">通知正文。</param>
        /// <param name="imageUri">Hero Image 的 URI（支持 ms-appx:// 或 https://）。</param>
        /// <param name="sound">音效配置。</param>
        public void ShowWithHeroImage(string title, string body, Uri imageUri, NotificationSound sound = NotificationSound.Default)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .SetHeroImage(imageUri);
                ApplySound(builder, sound);
                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.ShowWithHeroImage failed: " + ex.Message);
                FallbackToTray(title, body);
            }
        }

        /// <summary>
        /// 显示带 App Logo Override 的通知。
        /// </summary>
        /// <param name="title">通知标题。</param>
        /// <param name="body">通知正文。</param>
        /// <param name="logoUri">Logo 图片的 URI（支持 ms-appx:// 或 https://）。</param>
        /// <param name="sound">音效配置。</param>
        public void ShowWithAppLogo(string title, string body, Uri logoUri, NotificationSound sound = NotificationSound.Default)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .SetAppLogoOverride(logoUri);
                ApplySound(builder, sound);
                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.ShowWithAppLogo failed: " + ex.Message);
                FallbackToTray(title, body);
            }
        }

        /// <summary>
        /// 显示带进度条的通知，用于任务进度展示。
        /// 注意：SetProgressBar 当前仅在部分 Windows App SDK 版本中可用。
        /// </summary>
        /// <param name="title">通知标题。</param>
        /// <param name="body">通知正文/状态描述。</param>
        /// <param name="progress">进度值（0.0 ~ 1.0）。</param>
        /// <param name="sound">音效配置。</param>
        public void ShowProgress(string title, string body, double progress, NotificationSound sound = NotificationSound.Default)
        {
            try
            {
                var progressValue = Math.Clamp(progress, 0.0, 1.0);
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body);
                ApplySound(builder, sound);
                AppNotificationManager.Default.Show(builder.BuildNotification());
                Debug.WriteLine("NotificationService.ShowProgress: progress={0} (no progress bar API in this SDK version)", progressValue);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.ShowProgress failed: " + ex.Message);
                FallbackToTray(title, body);
            }
        }

        private static void ApplySound(AppNotificationBuilder builder, NotificationSound sound)
        {
            switch (sound)
            {
                case NotificationSound.Default:
                    builder.SetAudioEvent(AppNotificationSoundEvent.Default);
                    break;
                case NotificationSound.Call:
                    builder.SetAudioEvent(AppNotificationSoundEvent.Call);
                    break;
                case NotificationSound.Silent:
                    break;
            }
        }

        private static void FallbackToTray(string title, string body)
        {
            try
            {
                App.TrayService?.ShowNotification(title, body);
            }
            catch
            {
                // suppress
            }
        }

        private async void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            try
            {
                var arguments = args.Arguments;
                if (arguments == null)
                    return;

                if (!arguments.TryGetValue("action", out var action) || string.IsNullOrEmpty(action))
                    return;

                Debug.WriteLine("NotificationService: action received: " + action);

                switch (action)
                {
                    case "mark_as_read":
                        await HandleMarkAsRead(arguments);
                        break;
                    case "dismiss":
                        break;
                    case "open_window":
                        HandleOpenWindow();
                        break;
                    default:
                        Debug.WriteLine("NotificationService: unknown action: " + action);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.OnNotificationInvoked failed: " + ex.Message);
            }
        }

        private static async Task HandleMarkAsRead(IDictionary<string, string> arguments)
        {
            if (!arguments.TryGetValue("notificationId", out var notificationId) || string.IsNullOrEmpty(notificationId))
            {
                Debug.WriteLine("NotificationService: mark_as_read skipped, no notificationId");
                return;
            }

            Debug.WriteLine("NotificationService: sending notification-read for id=" + notificationId);

            try
            {
                await SocketIoService.Instance.SendEventAsync("notification-read", new
                {
                    eventId = $"read-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    notificationId = notificationId,
                    deviceInfo = new { deviceName = "桌面端", deviceType = "desktop" }
                });

                Debug.WriteLine("NotificationService: notification-read sent successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService: failed to send notification-read: " + ex.Message);
            }
        }

        private void HandleOpenWindow()
        {
            OpenWindowRequested?.Invoke();
        }

        /// <summary>
        /// 反注册通知服务，应在应用退出时调用。
        /// </summary>
        public void Unregister()
        {
            if (!_initialized || _disposed)
                return;

            try
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.Unregister();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationService.Unregister failed: " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Unregister();
            _instance = null;
        }
    }
}
