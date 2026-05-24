using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class UpdateSettingsModule : SettingsModuleBase
    {
        private TextBlock? _statusText;
        private Button? _checkButton;
        private ProgressRing? _progressRing;
        private TextBlock? _channelInfoBlock;
        private readonly UpdateService _updateService = new();

        public override string CategoryKey => "update";
        public override string Title => "更新";
        public override string Description => "选择更新渠道以获取不同版本的更新。";
        public override string Glyph => "\uE895";

        protected override FrameworkElement BuildContent()
        {
            var currentChannel = UpdateService.GetUpdateChannel();
            var currentCheckMode = UpdateService.GetUpdateCheckMode();

            _channelInfoBlock = new TextBlock
            {
                Text = $"当前更新渠道: {(currentChannel == "beta" ? "测试版 (Beta)" : "正式版 (Stable)")}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("版本控制",
                    CreateCheckUpdateRow(),
                    CreateAutoCheckRow(currentCheckMode),
                    SettingsUIHelper.CreateSettingRow("更新渠道", "选择获取更新的版本类型。", new FontIcon { Glyph = "\uE895" }, _channelInfoBlock),
                    CreateUpdateChannelRow("正式版 (Stable)", "获取稳定可靠的正式版本更新。", "stable", currentChannel == "stable"),
                    CreateUpdateChannelRow("测试版 (Beta)", "获取最新功能的测试版本，可能存在不稳定因素。", "beta", currentChannel == "beta")));
        }

        private Border CreateCheckUpdateRow()
        {
            var versionText = new TextBlock
            {
                Text = $"当前版本: {UpdateService.GetCurrentVersion()}",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium
            };

            _statusText = new TextBlock
            {
                Text = "点击按钮检查更新",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            _progressRing = new ProgressRing
            {
                IsActive = false,
                Width = 16,
                Height = 16,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };

            _checkButton = new Button
            {
                Content = "检查更新",
                VerticalAlignment = VerticalAlignment.Center
            };
            _checkButton.Click += async (_, _) => await CheckForUpdateAsync();

            var controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            controlPanel.Children.Add(_progressRing);
            controlPanel.Children.Add(_checkButton);

            var labelStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            labelStack.Children.Add(versionText);
            labelStack.Children.Add(_statusText);

            var contentGrid = new Grid { ColumnSpacing = 0 };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(labelStack, 0);
            Grid.SetColumn(controlPanel, 1);
            contentGrid.Children.Add(labelStack);
            contentGrid.Children.Add(controlPanel);

            return new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                Child = contentGrid
            };
        }

        private Border CreateAutoCheckRow(string currentMode)
        {
            var panel = new StackPanel { Spacing = 8 };

            var startupRadio = new RadioButton
            {
                IsChecked = currentMode == "startup",
                GroupName = "UpdateCheckMode",
                Content = "每次启动时自动检查并静默更新",
                VerticalAlignment = VerticalAlignment.Center
            };
            startupRadio.Checked += (_, _) => UpdateService.SetUpdateCheckMode("startup");

            var neverRadio = new RadioButton
            {
                IsChecked = currentMode == "never",
                GroupName = "UpdateCheckMode",
                Content = "从不更新",
                VerticalAlignment = VerticalAlignment.Center
            };
            neverRadio.Checked += (_, _) => UpdateService.SetUpdateCheckMode("never");

            panel.Children.Add(startupRadio);
            panel.Children.Add(neverRadio);

            return SettingsUIHelper.CreateSettingRow("自动检查更新", "每次启动时自动检查更新并在后台静默安装新版本。", new FontIcon { Glyph = "\uE916" }, panel);
        }

        private Border CreateUpdateChannelRow(string title, string description, string channelValue, bool isSelected)
        {
            var radioButton = new RadioButton
            {
                IsChecked = isSelected,
                GroupName = "UpdateChannel",
                Tag = channelValue,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            radioButton.Checked += (s, e) =>
            {
                if (s is RadioButton rb && rb.Tag is string newChannel)
                {
                    UpdateService.SetUpdateChannel(newChannel);
                    if (_channelInfoBlock != null)
                    {
                        _channelInfoBlock.Text = $"当前更新渠道: {(newChannel == "beta" ? "测试版 (Beta)" : "正式版 (Stable)")}";
                    }
                }
            };

            return SettingsUIHelper.CreateSettingRow(title, description, null, radioButton);
        }

        private async Task CheckForUpdateAsync()
        {
            if (_checkButton == null || _statusText == null || _progressRing == null)
                return;

            _checkButton.IsEnabled = false;
            _statusText.Text = "正在检查更新...";
            _progressRing.IsActive = true;
            _progressRing.Visibility = Visibility.Visible;

            try
            {
                var updateInfo = await _updateService.CheckForUpdateAsync();

                if (updateInfo == null)
                {
                    _statusText.Text = "检查更新失败，请稍后重试";
                    return;
                }

                if (!updateInfo.HasUpdate)
                {
                    _statusText.Text = "已是最新版本";
                    return;
                }

                _statusText.Text = $"发现新版本 {updateInfo.Version}";
                await ShowUpdateDialogAsync(updateInfo);
            }
            catch
            {
                _statusText.Text = "检查更新失败，请稍后重试";
            }
            finally
            {
                _checkButton.IsEnabled = true;
                _progressRing.IsActive = false;
                _progressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ShowUpdateDialogAsync(UpdateInfo updateInfo)
        {
            var dialog = new ContentDialog
            {
                Title = "发现新版本",
                XamlRoot = Context.Window.Content.XamlRoot,
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary
            };

            var content = new StackPanel { Spacing = 12 };
            dialog.Content = content;

            content.Children.Add(new TextBlock
            {
                Text = $"{updateInfo.Title} 可用",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            content.Children.Add(new TextBlock
            {
                Text = $"当前版本：{UpdateService.GetCurrentVersion()}，新版本：{updateInfo.Version}（大小：{updateInfo.FileSizeFormatted}）",
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            if (!string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes))
            {
                content.Children.Add(new TextBlock
                {
                    Text = "更新内容：",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                content.Children.Add(new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = updateInfo.ReleaseNotes,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    },
                    MaxHeight = 150,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                });
            }

            var downloadButton = new Button
            {
                Content = "下载并安装",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };
            downloadButton.Click += async (_, _) =>
            {
                dialog.Hide();
                await InstallUpdateAsync(updateInfo);
            };

            var manualDownloadButton = new Button
            {
                Content = "手动下载",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(8, 8, 0, 0)
            };
            manualDownloadButton.Click += (_, _) =>
            {
                try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(updateInfo.DownloadUrl)); } catch { }
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            buttonPanel.Children.Add(downloadButton);
            buttonPanel.Children.Add(manualDownloadButton);

            content.Children.Add(buttonPanel);

            await dialog.ShowAsync();
        }

        private async Task InstallUpdateAsync(UpdateInfo updateInfo)
        {
            var installer = new UpdateInstaller();
            var progressDialog = new ContentDialog
            {
                Title = "正在更新",
                Content = new StackPanel { Spacing = 12 },
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
                XamlRoot = Context.Window.Content.XamlRoot
            };

            var content = (StackPanel)progressDialog.Content;
            var statusText = new TextBlock { Text = "准备更新...", FontSize = 14 };
            content.Children.Add(statusText);

            var progressBar = new ProgressBar { IsIndeterminate = true, Height = 8 };
            content.Children.Add(progressBar);

            var progressText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            content.Children.Add(progressText);

            _ = progressDialog.ShowAsync();

            installer.StatusChanged += (_, status) => { statusText.Text = status; };
            installer.DownloadProgressChanged += (_, e) =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = e.Percentage;
                progressText.Text = $"{(e.DownloadedBytes / 1024.0 / 1024.0):F1} MB / {(e.TotalBytes / 1024.0 / 1024.0):F1} MB";
            };

            var result = await installer.DownloadAndInstallAsync(updateInfo);
            progressDialog.Hide();

            if (result.Success)
            {
                Logger.LogUpdate("更新成功");
                var successDialog = new ContentDialog
                {
                    Title = "更新成功",
                    Content = "应用将在几秒后自动重启",
                    CloseButtonText = "确定",
                    XamlRoot = Context.Window.Content.XamlRoot
                };
                _ = successDialog.ShowAsync();
                Environment.Exit(0);
            }
            else
            {
                Logger.LogUpdate("更新失败", result.ErrorMessage);
                var errorDialog = new ContentDialog
                {
                    Title = "更新失败",
                    Content = "无法完成更新，请尝试手动下载安装",
                    CloseButtonText = "确定",
                    XamlRoot = Context.Window.Content.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
        }
    }
}
