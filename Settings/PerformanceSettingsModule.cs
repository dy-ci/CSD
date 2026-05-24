using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;

using CSD.Models;
using CSD.Services;
using CSD.Helpers;

namespace CSD.Settings
{
    public class PerformanceSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "performance";
        public override string Title => "性能检测";
        public override string Description => "检测设备配置并提供专属优化建议，确保应用在不同设备上均能流畅运行。";
        public override string Glyph => "\uE773";

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

        private Button _optimizeBtn = null!;

        private ProgressRing _loadingRing = null!;
        private StackPanel _contentPanel = null!;

        private int _targetScore;

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

            _optimizeBtn = new Button
            {
                Content = "一键性能优化",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                Margin = new Thickness(0, 16, 0, 0)
            };
            _optimizeBtn.Click += OptimizeBtn_Click;

            var optCard = SettingsUIHelper.CreateSectionCard("动画与视觉效果", "自定义调整以平衡视觉体验与设备性能消耗。",
                SettingsUIHelper.CreateSettingRow("页面过渡动画", "开启页面切换时的平滑过渡动画。", null, _pageTransitionSwitch),
                SettingsUIHelper.CreateSettingRow("元素交互动画", "开启按钮、卡片等元素的悬浮与点击动画。", null, _elementInteractionSwitch),
                SettingsUIHelper.CreateSettingRow("背景模糊特效", "开启亚克力/Mica等背景模糊视觉效果。", null, _backgroundBlurSwitch),
                SettingsUIHelper.CreateSettingRow("高帧率渲染", "提升动画和滚动的渲染帧率。", null, _highFramerateSwitch),
                SettingsUIHelper.CreateSettingRow("高清资源加载", "加载更高分辨率的图片和资源。", null, _highResResourceSwitch),
                _optimizeBtn
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

        private static Grid CreateInfoGrid(params (string Label, TextBlock Value)[] rows)
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

        private async System.Threading.Tasks.Task LoadPerformanceDataAsync()
        {
            PerformanceInfo info;
            try
            {
                info = await System.Threading.Tasks.Task.Run(() => PerformanceService.GetPerformanceInfo());
            }
            catch
            {
                ShowContent();
                return;
            }

            _targetScore = info.Score;

            try
            {
                _scoreText.Text = "0";
                _scoreText.Foreground = PerformanceFormatter.GetScoreBrush(info.Score);
                _ratingText.Text = info.Rating;
                _ratingDescText.Text = info.RatingDescription;

                _mbText.Text = $"{info.Motherboard}\nBIOS: {info.BiosVersion}";
                _cpuText.Text = PerformanceFormatter.FormatCpuText(info.Cpus);
                _ramText.Text = PerformanceFormatter.FormatRamText(info);
                _gpuText.Text = PerformanceFormatter.FormatGpuText(info.Gpus);
                _storageText.Text = PerformanceFormatter.FormatStorageText(info.Drives, info.LogicalDrives);
                _screenText.Text = PerformanceFormatter.FormatDisplayText(info.Displays);

                _osText.Text = PerformanceFormatter.FormatOsText(info);
                _browserText.Text = info.BrowserInfo;
                _servicesText.Text = $"{info.RunningServicesCount} 个";
                _processesText.Text = PerformanceFormatter.FormatProcessesText(info.TotalProcessesCount, info.BackgroundProcessRatio);
                _appUsageText.Text = PerformanceFormatter.FormatAppUsageText(info.AppMemoryMb);

                _optimizeBtn.Content = PerformanceFormatter.GetOptimizeButtonText(info.Score);

                ShowContent();
                AnimateScore();
            }
            catch
            {
                ShowContent();
            }
        }

        private void AnimateScore()
        {
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(16);
            int current = 0;
            int steps = Math.Max(1, _targetScore / 3);

            timer.Tick += (s, e) =>
            {
                current += Math.Max(1, (_targetScore - current + steps - 1) / steps);
                if (current >= _targetScore)
                {
                    current = _targetScore;
                    timer.Stop();
                }
                _scoreText.Text = current.ToString();
            };

            timer.Start();
        }

        private void ShowContent()
        {
            if (_loadingRing == null || _contentPanel == null) return;
            _loadingRing.IsActive = false;
            _loadingRing.Visibility = Visibility.Collapsed;
            _contentPanel.Visibility = Visibility.Visible;
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            _pageTransitionSwitch.IsOn = settings.GetBool("Settings_PageTransitionAnimations", true);
            _elementInteractionSwitch.IsOn = settings.GetBool("Settings_ElementInteractionAnimations", true);
            _backgroundBlurSwitch.IsOn = settings.GetBool("Settings_BackgroundBlurEffects", true);
            _highFramerateSwitch.IsOn = settings.GetBool("Settings_HighFramerateRendering", true);
            _highResResourceSwitch.IsOn = settings.GetBool("Settings_HighResResourceLoading", true);
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
            var presets = PerformanceFormatter.GetOptimizationPresets(0);
            _pageTransitionSwitch.IsOn = presets.PageAnim;
            _elementInteractionSwitch.IsOn = presets.InterAnim;
            _backgroundBlurSwitch.IsOn = presets.Blur;
            _highFramerateSwitch.IsOn = presets.HighRate;
            _highResResourceSwitch.IsOn = presets.HighRes;
            NotifySettingsChanged();
        }
    }
}
