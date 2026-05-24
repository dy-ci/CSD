using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics;

using CSD.Helpers;
using CSD.Services;

namespace CSD.Views
{
    public sealed partial class NotificationSendWindow : Window
    {
        private readonly List<string> _sendHistory = new();

        public NotificationSendWindow()
        {
            InitializeComponent();
            TouchKeyboardHelper.EnableForControl(ContentBox);
            VisualHelper.ApplyWindowBackdrop(this);

            ExtendsContentIntoTitleBar = true;
            this.AppWindow.Resize(new SizeInt32(600, 700));

            try
            {
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch { }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var content = ContentBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                await ShowDialog("提示", "请输入通知内容");
                return;
            }

            if (!SocketIoService.Instance.IsConnected)
            {
                await ShowDialog("未连接", "未连接到服务器，请检查网络连接");
                return;
            }

            var isUrgent = UrgentToggle.IsOn;
            var notificationId = GenerateNotificationId();
            var eventId = $"evt-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString("N")[..8]}";

            try
            {
                SendButton.IsEnabled = false;
                SendButton.Content = "发送中...";

                await SocketIoService.Instance.SendEventAsync(
                    isUrgent ? "urgent-notice" : "notification",
                    new
                    {
                        eventId,
                        notificationId,
                        message = content,
                        isUrgent,
                        targetDevices = new[] { "classroom", "teacher", "desktop" },
                        senderInfo = new
                        {
                            deviceName = "桌面端",
                            deviceType = "desktop",
                            isReadOnly = false
                        }
                    }
                );

                _sendHistory.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {content}");
                UpdateHistoryPanel();

                ContentBox.Text = string.Empty;
                UrgentToggle.IsOn = false;
                PersistentCheckBox.IsChecked = false;

                await ShowDialog("发送成功", "通知已发送");
            }
            catch (Exception ex)
            {
                await ShowDialog("发送失败", ex.Message);
            }
            finally
            {
                SendButton.IsEnabled = true;
                SendButton.Content = "\u24D8 发送通知";
            }
        }

        private async System.Threading.Tasks.Task ShowDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private static string GenerateNotificationId()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 32).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        private void UpdateHistoryPanel()
        {
            if (_sendHistory.Count > 0)
            {
                HistoryEmptyPanel.Visibility = Visibility.Collapsed;
                HistoryScrollViewer.Visibility = Visibility.Visible;
                HistoryListPanel.Children.Clear();

                foreach (var msg in _sendHistory)
                {
                    var tb = new TextBlock
                    {
                        Text = msg,
                        TextWrapping = TextWrapping.Wrap,
                        Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    HistoryListPanel.Children.Add(tb);
                }
            }
            else
            {
                HistoryEmptyPanel.Visibility = Visibility.Visible;
                HistoryScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
        }
    }
}
