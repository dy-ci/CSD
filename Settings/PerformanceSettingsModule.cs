using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;

using CSD.Models;
using CSD.Services;

namespace CSD.Settings
{
    public class PerformanceSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "performance";
        public override string Title => "性能检测";
        public override string Description => "检测设备配置并提供专属优化建议，确保应用在不同设备上均能流畅运行。";
        public override string Glyph => "\uE773"; // Speedometer icon

        private ToggleSwitch _pageTransitionSwitch = null!;
        private ToggleSwitch _elementInteractionSwitch = null!;
        private ToggleSwitch _backgroundBlurSwitch = null!;
        private ToggleSwitch _highFramerateSwitch = null!;
        private ToggleSwitch _highResResourceSwitch = null!;
        
        private TextBlock _scoreText = null!;
        private TextBlock _ratingText = null!;
        private TextBlock _ratingDescText = null!;
        
        private TextBlock _mbText = null!;
        private TextBlock _cpuText = null!;
        private TextBlock _ramText = null!;
        private TextBlock _gpuText = null!;
        private TextBlock _storageText = null!;
        private TextBlock _screenText = null!;
        
        private TextBlock _osText = null!;
        private TextBlock _browserText = null!;
        private TextBlock _servicesText = null!;
        private TextBlock _processesText = null!;
        private TextBlock _appUsageText = null!;
        
        private ProgressRing _loadingRing = null!;
        private StackPanel _contentPanel = null!;

        protected override FrameworkElement BuildContent()
        {
            // Performance Score Section
            _scoreText = new TextBlock { FontSize = 48, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            _ratingText = new TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) };
            _ratingDescText = SettingsUIHelper.CreateSecondaryWrappedText("");

            var scorePanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            scorePanel.Children.Add(_scoreText);
            scorePanel.Children.Add(_ratingText);
            scorePanel.Children.Add(_ratingDescText);
            
            var scoreCard = SettingsUIHelper.CreateSectionCard("性能评级", "基于当前硬件和软件运行状态的综合得分", scorePanel);

            // Hardware Config Section
            _mbText = new TextBlock();
            _cpuText = new TextBlock();
            _ramText = new TextBlock();
            _gpuText = new TextBlock();
            _storageText = new TextBlock();
            _screenText = new TextBlock();

            var hwGrid = CreateInfoGrid(
                ("主板设备:", _mbText),
                ("CPU处理器:", _cpuText),
                ("运行内存:", _ramText),
                ("显卡GPU:", _gpuText),
                ("存储设备:", _storageText),
                ("显示器:", _screenText)
            );
            var hwCard = SettingsUIHelper.CreateSectionCard("硬件配置", "当前设备的核心硬件详细参数", hwGrid);

            // Software Config Section
            _osText = new TextBlock();
            _browserText = new TextBlock();
            _servicesText = new TextBlock();
            _processesText = new TextBlock();
            _appUsageText = new TextBlock();

            var swGrid = CreateInfoGrid(
                ("操作系统:", _osText),
                ("默认浏览器:", _browserText),
                ("运行中服务:", _servicesText),
                ("后台进程占比:", _processesText),
                ("当前应用资源:", _appUsageText)
            );
            var swCard = SettingsUIHelper.CreateSectionCard("软件环境", "当前系统软件运行状态数据", swGrid);

            // Optimization Section
            _pageTransitionSwitch = new ToggleSwitch { OnContent = "开", OffContent = "关" };
            _elementInteractionSwitch = new ToggleSwitch { OnContent = "开", OffContent = "关" };
            _backgroundBlurSwitch = new ToggleSwitch { OnContent = "开", OffContent = "关" };
            _highFramerateSwitch = new ToggleSwitch { OnContent = "开", OffContent = "关" };
            _highResResourceSwitch = new ToggleSwitch { OnContent = "开", OffContent = "关" };

            var optimizeBtn = new Button 
            { 
                Content = "一键性能优化 (老设备专属)", 
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                Margin = new Thickness(0, 16, 0, 0)
            };
            optimizeBtn.Click += OptimizeBtn_Click;

            var optCard = SettingsUIHelper.CreateSectionCard("动画与视觉效果", "自定义调整以平衡视觉体验与设备性能消耗。",
                SettingsUIHelper.CreateSettingRow("页面过渡动画", "开启页面切换时的平滑过渡动画。", _pageTransitionSwitch),
                SettingsUIHelper.CreateSettingRow("元素交互动画", "开启按钮、卡片等元素的悬浮与点击动画。", _elementInteractionSwitch),
                SettingsUIHelper.CreateSettingRow("背景模糊特效", "开启亚克力/Mica等背景模糊视觉效果。", _backgroundBlurSwitch),
                SettingsUIHelper.CreateSettingRow("高帧率渲染", "提升动画和滚动的渲染帧率。", _highFramerateSwitch),
                SettingsUIHelper.CreateSettingRow("高清资源加载", "加载更高分辨率的图片和资源。", _highResResourceSwitch),
                optimizeBtn
            );

            _contentPanel = new StackPanel { Spacing = 16, Visibility = Visibility.Collapsed };
            _contentPanel.Children.Add(scoreCard);
            _contentPanel.Children.Add(hwCard);
            _contentPanel.Children.Add(swCard);
            _contentPanel.Children.Add(optCard);

            _loadingRing = new ProgressRing { IsActive = true, Width = 40, Height = 40, Margin = new Thickness(0, 40, 0, 0) };

            var root = new Grid();
            root.Children.Add(_loadingRing);
            root.Children.Add(_contentPanel);
            
            _ = LoadPerformanceDataAsync();

            return SettingsUIHelper.CreateCategoryView(root);
        }

        private Grid CreateInfoGrid(params (string Label, TextBlock Value)[] rows)
        {
            var grid = new Grid { ColumnSpacing = 16, RowSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < rows.Length; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var label = new TextBlock { Text = rows[i].Label, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
                Grid.SetRow(label, i);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var val = rows[i].Value;
                val.TextWrapping = TextWrapping.Wrap;
                Grid.SetRow(val, i);
                Grid.SetColumn(val, 1);
                grid.Children.Add(val);
            }

            return grid;
        }

        private async Task LoadPerformanceDataAsync()
        {
            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            PerformanceInfo info;
            try
            {
                info = await Task.Run(() => PerformanceService.GetPerformanceInfo());
            }
            catch
            {
                dq?.TryEnqueue(() =>
                {
                    _loadingRing.IsActive = false;
                    _loadingRing.Visibility = Visibility.Collapsed;
                    _contentPanel.Visibility = Visibility.Visible;
                });
                return;
            }

            dq?.TryEnqueue(() =>
            {
                try
                {
                    _scoreText.Text = info.Score.ToString();
                    _ratingText.Text = info.Rating;
                    _ratingDescText.Text = info.RatingDescription;

                    if (info.Score <= 40)
                        _scoreText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
                    else if (info.Score <= 60)
                        _scoreText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                    else if (info.Score <= 80)
                        _scoreText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    else
                        _scoreText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);

                    _mbText.Text = $"{info.Motherboard}\nBIOS: {info.BiosVersion}";

                    if (info.Cpus.Count == 0)
                    {
                        _cpuText.Text = "未检测到处理器";
                    }
                    else
                    {
                        var cpuTexts = info.Cpus.Select((c, i) => $"[{i + 1}] {c.Model}\n    {c.Cores} 核心 / {c.LogicalProcessors} 线程 | {c.Frequency} | L2: {c.L2CacheKb/1024.0:F1} MB | L3: {c.L3CacheKb/1024.0:F1} MB");
                        _cpuText.Text = string.Join("\n", cpuTexts);
                    }

                    _ramText.Text = $"总计: {info.RamTotalGb:F1} GB | 可用: {info.RamAvailableGb:F1} GB\n类型: {info.RamType} {info.RamSpeed} MHz | 插槽使用: {info.RamSlotsUsed}";

                    if (info.Gpus.Count == 0)
                    {
                        _gpuText.Text = "未检测到显卡";
                    }
                    else
                    {
                        var gpuTexts = info.Gpus.Select((g, i) => $"[{i + 1}] {g.Name}\n    显存: {g.VramGb:F1} GB | 驱动: {g.DriverVersion}");
                        _gpuText.Text = string.Join("\n", gpuTexts);
                    }

                    var phys = info.Drives.Select((d, i) => $"[物理磁盘] {d.Name} ({d.MediaType}) - {d.TotalGb:F0} GB");
                    var log = info.LogicalDrives.Select(d => $"[逻辑分区] {d.Letter} {d.AvailableGb:F0} GB 可用 / {d.TotalGb:F0} GB 总计");
                    _storageText.Text = string.Join("\n", phys) + "\n" + string.Join("\n", log);
                    
                    if (info.Displays.Count == 0)
                    {
                        _screenText.Text = "未检测到显示器";
                    }
                    else
                    {
                        var displayTexts = info.Displays.Select((d, i) => $"[显示器 {i + 1}] {d.Resolution} @ {d.RefreshRate} Hz");
                        _screenText.Text = string.Join("\n", displayTexts);
                    }

                    _osText.Text = $"{info.OsVersion} | 架构: {info.OsArchitecture}";
                    _browserText.Text = info.BrowserInfo;
                    _servicesText.Text = $"{info.RunningServicesCount} 个";
                    _processesText.Text = $"{(info.BackgroundProcessRatio * 100):F1}% (总进程数 {info.TotalProcessesCount})";
                    _appUsageText.Text = $"内存占用: {info.AppMemoryMb:F1} MB";

                    _loadingRing.IsActive = false;
                    _loadingRing.Visibility = Visibility.Collapsed;
                    _contentPanel.Visibility = Visibility.Visible;
                }
                catch
                {
                    // Window may have been closed — ignore stale UI updates
                }
            });
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            _pageTransitionSwitch.IsOn = (bool)(settings["Settings_PageTransitionAnimations"] ?? true);
            _elementInteractionSwitch.IsOn = (bool)(settings["Settings_ElementInteractionAnimations"] ?? true);
            _backgroundBlurSwitch.IsOn = (bool)(settings["Settings_BackgroundBlurEffects"] ?? true);
            _highFramerateSwitch.IsOn = (bool)(settings["Settings_HighFramerateRendering"] ?? true);
            _highResResourceSwitch.IsOn = (bool)(settings["Settings_HighResResourceLoading"] ?? true);
        }

        protected override void HookAutoSaveHandlers()
        {
            _pageTransitionSwitch.Toggled += (_, _) => NotifySettingsChanged();
            _elementInteractionSwitch.Toggled += (_, _) => NotifySettingsChanged();
            _backgroundBlurSwitch.Toggled += (_, _) => NotifySettingsChanged();
            _highFramerateSwitch.Toggled += (_, _) => NotifySettingsChanged();
            _highResResourceSwitch.Toggled += (_, _) => NotifySettingsChanged();
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;
            settings["Settings_PageTransitionAnimations"] = _pageTransitionSwitch.IsOn;
            settings["Settings_ElementInteractionAnimations"] = _elementInteractionSwitch.IsOn;
            settings["Settings_BackgroundBlurEffects"] = _backgroundBlurSwitch.IsOn;
            settings["Settings_HighFramerateRendering"] = _highFramerateSwitch.IsOn;
            settings["Settings_HighResResourceLoading"] = _highResResourceSwitch.IsOn;
        }

        private void OptimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            _pageTransitionSwitch.IsOn = false;
            _elementInteractionSwitch.IsOn = false;
            _backgroundBlurSwitch.IsOn = false;
            _highFramerateSwitch.IsOn = false;
            _highResResourceSwitch.IsOn = false;
            NotifySettingsChanged();
        }
    }
}