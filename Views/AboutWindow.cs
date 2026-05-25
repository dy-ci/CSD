using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;




namespace CSD.Views
{
    public sealed class AboutWindow : Window
    {
        private readonly UpdateService _updateService = new();
        private Button? _checkUpdateButton;
        private TextBlock? _updateStatusText;
        private ProgressRing? _updateProgressRing;

        public AboutWindow()
        {
            Title = "关于 Classworks Desktop";
            SystemBackdrop = new MicaBackdrop();

            var root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                }
            };

            var heroBorder = new Border
            {
                Padding = new Thickness(32, 40, 32, 32),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetRow(heroBorder, 0);

            var heroStack = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };

            var iconImage = new Image
            {
                Width = 72,
                Height = 72,
                Source = new BitmapImage(AppSettings.GetAssetUri("Assets/Classworks.ico")) { DecodePixelWidth = 72, DecodePixelHeight = 72 },
                HorizontalAlignment = HorizontalAlignment.Center
            };
            heroStack.Children.Add(iconImage);

            heroStack.Children.Add(new TextBlock
            {
                Text = "Classworks Desktop",
                FontSize = 32,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            heroStack.Children.Add(new TextBlock
            {
                Text = GetAppVersion(),
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -4, 0, 0)
            });

            heroBorder.Child = heroStack;
            root.Children.Add(heroBorder);

            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 0, 24, 24)
            };
            Grid.SetRow(contentScroll, 1);

            var contentStack = new StackPanel { Spacing = 16 };

            contentStack.Children.Add(CreateSectionCard(
                "应用简介",
                "Classworks Desktop 是一款桌面应用程序，为 Classworks 提供原生桌面体验。支持作业管理、Markdown/MFM 富文本编辑与渲染等功能。"
            ));

            var creditsPanel = new StackPanel { Spacing = 10 };
            creditsPanel.Children.Add(CreateCreditItem("翟十光", "客户端开发者", AppSettings.GetAssetUri("Assets/zhaishis.png").AbsoluteUri));
            creditsPanel.Children.Add(CreateCreditItem("Saskia", "提供了开发环境和 Token", AppSettings.GetAssetUri("Assets/saskia.jpeg").AbsoluteUri));
            creditsPanel.Children.Add(CreateCreditItem("孙悟元", "Classworks 开发者", AppSettings.GetAssetUri("Assets/wuyuan.jpeg").AbsoluteUri));
            contentStack.Children.Add(CreateSectionCard("致谢", creditsPanel));

            var linksPanel = new StackPanel { Spacing = 8 };
            linksPanel.Children.Add(CreateLinkItem("GitHub 仓库", "https://github.com/dlasspro/CSD", "\uE8F4"));
            linksPanel.Children.Add(CreateLinkItem("官方网站", "https://cs.dy.ci", "\uE774"));
            contentStack.Children.Add(CreateSectionCard("链接", linksPanel));

            var updatePanel = new StackPanel { Spacing = 10 };
            var updateHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            updateHeader.Children.Add(new TextBlock
            {
                Text = "检查更新",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            _updateProgressRing = new ProgressRing
            {
                IsActive = false,
                Width = 16,
                Height = 16,
                Visibility = Visibility.Collapsed
            };
            updateHeader.Children.Add(_updateProgressRing);
            _updateStatusText = new TextBlock
            {
                Text = $"当前版本 {GetAppVersion()}",
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            _checkUpdateButton = new Button
            {
                Content = "检查更新",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _checkUpdateButton.Click += async (_, _) => await CheckForUpdateAsync();
            updatePanel.Children.Add(updateHeader);
            updatePanel.Children.Add(_updateStatusText);
            updatePanel.Children.Add(_checkUpdateButton);
            contentStack.Children.Add(CreateSectionCard("软件更新", updatePanel));

            var techPanel = new StackPanel { Spacing = 6 };
            techPanel.Children.Add(CreateInfoRow("框架", "WinUI 3 / Windows App SDK 2.0"));
            techPanel.Children.Add(CreateInfoRow("运行时", ".NET 8"));
            techPanel.Children.Add(CreateInfoRow("后端", "KV 存储服务"));
            techPanel.Children.Add(CreateInfoRow("渲染", "Markdown + MFM"));
            contentStack.Children.Add(CreateSectionCard("技术栈", techPanel));

            contentStack.Children.Add(new TextBlock
            {
                Text = "\u00A9 2026 dy.ci. All rights reserved.",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });

            contentScroll.Content = contentStack;
            root.Children.Add(contentScroll);

            Content = root;
            root.Loaded += async (_, _) =>
            {
                AnimationHelper.AnimateEntrance(root, fromY: 18f, durationMs: 360);
                AnimationHelper.ApplyStandardInteractions(contentScroll);
                _ = CheckForUpdateAsync();
            };

            AppWindow.Resize(new Windows.Graphics.SizeInt32(440, 680));
        }

        private async Task CheckForUpdateAsync()
        {
            if (_checkUpdateButton == null || _updateStatusText == null || _updateProgressRing == null)
                return;

            _checkUpdateButton.IsEnabled = false;
            _updateStatusText.Text = "正在检查更新...";
            _updateProgressRing.IsActive = true;
            _updateProgressRing.Visibility = Visibility.Visible;

            try
            {
                var updateInfo = await _updateService.CheckForUpdateAsync();

                if (updateInfo == null)
                {
                    _updateStatusText.Text = "检查更新失败，请稍后重试";
                    return;
                }

                if (!updateInfo.HasUpdate)
                {
                    _updateStatusText.Text = "已是最新版本";
                    return;
                }

                await ShowUpdateDialogAsync(updateInfo);
            }
            catch
            {
                _updateStatusText.Text = "检查更新失败，请稍后重试";
            }
            finally
            {
                _checkUpdateButton.IsEnabled = true;
                _updateProgressRing.IsActive = false;
                _updateProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ShowUpdateDialogAsync(UpdateInfo updateInfo)
        {
            var dialog = new ContentDialog
            {
                Title = "发现新版本",
                Content = new StackPanel { Spacing = 12 },
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var content = (StackPanel)dialog.Content;

            content.Children.Add(new TextBlock
            {
                Text = $"{updateInfo.Title} 可用",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            content.Children.Add(new TextBlock
            {
                Text = $"当前版本：{GetAppVersion()}",
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            content.Children.Add(new TextBlock
            {
                Text = $"新版本：{updateInfo.Version} (大小：{updateInfo.FileSizeFormatted})",
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

                var releaseNotesScroll = new ScrollViewer
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
                };
                content.Children.Add(releaseNotesScroll);
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
                try
                {
                    _ = Windows.System.Launcher.LaunchUriAsync(new Uri(updateInfo.DownloadUrl));
                }
                catch { }
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
                XamlRoot = Content.XamlRoot
            };

            var content = (StackPanel)progressDialog.Content;
            var statusText = new TextBlock
            {
                Text = "准备更新...",
                FontSize = 14
            };
            content.Children.Add(statusText);

            var progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 8
            };
            content.Children.Add(progressBar);

            var progressText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            content.Children.Add(progressText);

            _ = progressDialog.ShowAsync();

            installer.StatusChanged += (_, status) =>
            {
                statusText.Text = status;
            };

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
                    XamlRoot = Content.XamlRoot
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
                    XamlRoot = Content.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
        }

        private static string GetAppVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"v{version?.Major ?? 0}.{version?.Minor ?? 0}.{version?.Build ?? 0}.{version?.Revision ?? 0}";
            }
            catch
            {
                return "v1.0.0.0";
            }
        }

        private static Border CreateSectionCard(string title, string description)
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            return new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Child = panel
            };
        }

        private static Border CreateSectionCard(string title, UIElement content)
        {
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            panel.Children.Add(content);

            return new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Child = panel
            };
        }

        private static StackPanel CreateCreditItem(string name, string role, string avatarUri)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12
            };

            panel.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Child = new Image
                {
                    Source = new BitmapImage(new Uri(avatarUri)) { DecodePixelWidth = 32, DecodePixelHeight = 32 },
                    Stretch = Stretch.UniformToFill
                }
            });

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoPanel.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = role,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            });

            panel.Children.Add(infoPanel);
            return panel;
        }

        private static Button CreateLinkItem(string title, string url, string glyph)
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4)
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };
            panel.Children.Add(new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });

            btn.Content = panel;
            btn.Click += (_, _) =>
            {
                try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url)); } catch { }
            };

            return btn;
        }

        private static StackPanel CreateInfoRow(string label, string value)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                MinWidth = 60
            });
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            return panel;
        }
    }
}
