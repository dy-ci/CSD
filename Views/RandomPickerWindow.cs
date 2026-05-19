using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;




namespace CSD.Views
{
    public sealed partial class RandomPickerWindow : Window
    {
        private readonly HttpClient _httpClient = new();

        private readonly StackPanel _rootPanel;
        private readonly Border _windowRoot;
        private readonly TextBlock _countDisplay;
        private readonly Button _btnDecrease;
        private readonly Button _btnIncrease;
        private readonly Button _btnNameMode;
        private readonly Button _btnNumberMode;
        private readonly Button _btnStartPick;
        private readonly TextBlock _availableCountText;
        private readonly Button _btnIncludeLate;
        private readonly Button _btnExcludeLeave;
        private readonly Button _btnExcludeAbsent;
        private readonly Border _animationOverlay;
        private readonly StackPanel _overlayContent;
        private readonly TextBlock _animationText;
        private readonly TextBlock _resultText;
        private readonly StackPanel _resultListPanel;
        private readonly TextBlock _dismissHintText;

        private readonly Grid _appTitleBar;
        private readonly ColumnDefinition _leftInsetColumn;
        private readonly ColumnDefinition _rightInsetColumn;

        private int _pickCount = 1;
        private bool _isNameMode = true;
        private bool _includeLate = true;
        private bool _excludeLeave = true;
        private bool _excludeAbsent = true;

        private List<string> _studentNames = new();
        private List<string> _availableStudents = new();
        private Random _random = new();

        private readonly SolidColorBrush _accentBrush;
        private readonly SolidColorBrush _accentForeground;
        private readonly SolidColorBrush _transparentBrush;
        private readonly SolidColorBrush _secondaryTextBrush;
        private readonly SolidColorBrush _primaryTextBrush;

        public RandomPickerWindow()
        {
            Title = "随机点名";

            AppWindow.Resize(new SizeInt32(680, 620));
            SystemBackdrop = new MicaBackdrop();

            _accentBrush = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            _accentForeground = (SolidColorBrush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
            _transparentBrush = new SolidColorBrush(Colors.Transparent);
            _secondaryTextBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            _primaryTextBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

            _isNameMode = true;
            _pickCount = RandomPickerSettings.DefaultCount;
            _includeLate = true;
            _excludeLeave = true;
            _excludeAbsent = true;

            _rootPanel = new StackPanel { Spacing = 0 };

            _leftInsetColumn = new ColumnDefinition { Width = new GridLength(0) };
            _rightInsetColumn = new ColumnDefinition { Width = new GridLength(0) };

            _appTitleBar = new Grid
            {
                MinHeight = 52,
                Padding = new Thickness(0, 8, 0, 8)
            };
            _appTitleBar.ColumnDefinitions.Add(_leftInsetColumn);
            _appTitleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _appTitleBar.ColumnDefinitions.Add(_rightInsetColumn);

            var titleBarTextStack = new StackPanel
            {
                Margin = new Thickness(24, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Orientation = Orientation.Horizontal,
                Spacing = 12
            };
            titleBarTextStack.Children.Add(new FontIcon
            {
                Glyph = "\uE716",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            });
            titleBarTextStack.Children.Add(new TextBlock
            {
                Text = "随机点名",
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            Grid.SetColumn(titleBarTextStack, 1);
            _appTitleBar.Children.Add(titleBarTextStack);

            var contentPanel = new StackPanel
            {
                Spacing = 24,
                Padding = new Thickness(32, 8, 32, 32)
            };

            var countSection = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center };
            countSection.Children.Add(new TextBlock
            {
                Text = "请选择抽取人数",
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var countPickerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnDecrease = CreateRoundButton("\uE949", false);
            _btnDecrease.Click += DecreaseCount_Click;

            _countDisplay = new TextBlock
            {
                Text = _pickCount.ToString(),
                FontSize = 56,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = _primaryTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 80,
                TextAlignment = TextAlignment.Center
            };

            var countUnit = new TextBlock
            {
                Text = "人",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = _secondaryTextBrush
            };

            var countRight = new StackPanel { Spacing = 0, Orientation = Orientation.Horizontal };
            countRight.Children.Add(_countDisplay);
            countRight.Children.Add(countUnit);

            _btnIncrease = CreateRoundButton("\uE710", true);
            _btnIncrease.Click += IncreaseCount_Click;

            countPickerRow.Children.Add(_btnDecrease);
            countPickerRow.Children.Add(countRight);
            countPickerRow.Children.Add(_btnIncrease);
            countSection.Children.Add(countPickerRow);
            contentPanel.Children.Add(countSection);

            var modeRow = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 0 };
            var modeBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4)
            };
            var modeGrid = new Grid();
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnNameMode = CreateModeButton("姓名模式", "\uE716", true);
            Grid.SetColumn(_btnNameMode, 0);
            _btnNameMode.Click += (_, _) => SetMode(true);
            modeGrid.Children.Add(_btnNameMode);

            _btnNumberMode = CreateModeButton("学号模式", "\uE949", false);
            Grid.SetColumn(_btnNumberMode, 1);
            _btnNumberMode.Click += (_, _) => SetMode(false);
            modeGrid.Children.Add(_btnNumberMode);

            modeBorder.Child = modeGrid;
            modeRow.Children.Add(modeBorder);
            contentPanel.Children.Add(modeRow);

            _btnStartPick = new Button
            {
                Content = CreateStartButtonContent(),
                Background = _accentBrush,
                Foreground = _accentForeground,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(32, 16, 32, 16),
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 200,
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            _btnStartPick.Click += StartPick_Click;
            contentPanel.Children.Add(_btnStartPick);

            _availableCountText = new TextBlock
            {
                Text = "当前可抽取学生: 0人",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = _secondaryTextBrush
            };
            contentPanel.Children.Add(_availableCountText);

            var filterRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _btnIncludeLate = CreateFilterButton("\uE823", "包含迟到学生", true, true);
            _btnIncludeLate.Click += (_, _) => ToggleFilter(_btnIncludeLate, ref _includeLate);
            filterRow.Children.Add(_btnIncludeLate);

            _btnExcludeLeave = CreateFilterButton("\uE716", "排除请假学生", false, false);
            _btnExcludeLeave.Click += (_, _) => ToggleFilter(_btnExcludeLeave, ref _excludeLeave);
            filterRow.Children.Add(_btnExcludeLeave);

            _btnExcludeAbsent = CreateFilterButton("\uE716", "排除不参与学生", false, false);
            _btnExcludeAbsent.Click += (_, _) => ToggleFilter(_btnExcludeAbsent, ref _excludeAbsent);
            filterRow.Children.Add(_btnExcludeAbsent);

            contentPanel.Children.Add(filterRow);

            _rootPanel.Children.Add(contentPanel);

            _dismissHintText = new TextBlock
            {
                Text = "点击空白处返回抽取界面",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };

            _animationText = new TextBlock
            {
                FontSize = 72,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _resultText = new TextBlock
            {
                FontSize = 48,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            _resultListPanel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };

            var overlayResultPanel = new StackPanel
            {
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            overlayResultPanel.Children.Add(_animationText);
            overlayResultPanel.Children.Add(_resultText);
            overlayResultPanel.Children.Add(_resultListPanel);

            _overlayContent = new StackPanel
            {
                Spacing = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _overlayContent.Children.Add(overlayResultPanel);
            _overlayContent.Children.Add(_dismissHintText);

            _animationOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = _overlayContent
            };
            _animationOverlay.Tapped += AnimationOverlay_Tapped;

            _windowRoot = new Border
            {
                Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"],
                Child = _rootPanel
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_appTitleBar, 0);
            Grid.SetRow(_windowRoot, 1);
            root.Children.Add(_appTitleBar);
            root.Children.Add(_windowRoot);

            var gridRoot = new Grid();
            gridRoot.Children.Add(root);
            gridRoot.Children.Add(_animationOverlay);

            Content = gridRoot;

            ConfigureIntegratedTitleBar();

            _ = LoadStudentsAsync();
        }

        private void ConfigureIntegratedTitleBar()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
                return;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(_appTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            UpdateTitleBarLayout(AppWindow.TitleBar);

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

        private void UpdateTitleBarLayout(AppWindowTitleBar titleBar)
        {
            _leftInsetColumn.Width = new GridLength(titleBar.LeftInset);
            _rightInsetColumn.Width = new GridLength(titleBar.RightInset);
        }

        private void AnimationOverlay_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            _animationOverlay.Visibility = Visibility.Collapsed;
            _btnStartPick.IsEnabled = true;
        }

        private static UIElement CreateStartButtonContent()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new FontIcon { Glyph = "\uE723", FontSize = 22, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = "开始抽取", VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        private Button CreateRoundButton(string glyph, bool isPlus)
        {
            return new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 18 },
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(28),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Foreground = isPlus ? _accentBrush : _secondaryTextBrush
            };
        }

        private Button CreateModeButton(string label, string glyph, bool isActive)
        {
            return new Button
            {
                Content = CreateModeButtonContent(label, glyph),
                Background = isActive ? _accentBrush : _transparentBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 10, 16, 10),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = isActive ? _accentForeground : _secondaryTextBrush
            };
        }

        private static UIElement CreateModeButtonContent(string label, string glyph)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        private Button CreateFilterButton(string glyph, string label, bool isActive, bool isAccent)
        {
            var bg = isActive
                ? new SolidColorBrush(Color.FromArgb(100, 200, 80, 80))
                : isAccent ? (Brush)_accentBrush : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            var fg = isActive
                ? new SolidColorBrush(Colors.White)
                : isAccent ? (Brush)_accentForeground : (Brush)_secondaryTextBrush;

            return new Button
            {
                Content = CreateFilterButtonContent(glyph, label),
                Background = bg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = fg
            };
        }

        private static UIElement CreateFilterButtonContent(string glyph, string label)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        private void DecreaseCount_Click(object sender, RoutedEventArgs e)
        {
            if (_pickCount > 1)
            {
                _pickCount--;
                _countDisplay.Text = _pickCount.ToString();
            }
        }

        private void IncreaseCount_Click(object sender, RoutedEventArgs e)
        {
            if (_pickCount < 20)
            {
                _pickCount++;
                _countDisplay.Text = _pickCount.ToString();
            }
        }

        private void SetMode(bool isName)
        {
            _isNameMode = isName;
            _btnNameMode.Background = isName ? _accentBrush : _transparentBrush;
            _btnNumberMode.Background = !isName ? _accentBrush : _transparentBrush;
            _btnNameMode.Foreground = isName ? _accentForeground : _secondaryTextBrush;
            _btnNumberMode.Foreground = !isName ? _accentForeground : _secondaryTextBrush;
            UpdateAvailableCount();
        }

        private void ToggleFilter(Button btn, ref bool value)
        {
            value = !value;
            btn.Background = value
                ? new SolidColorBrush(Color.FromArgb(100, 200, 80, 80))
                : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            btn.Foreground = value ? new SolidColorBrush(Colors.White) : _secondaryTextBrush;
        }

        private async Task LoadStudentsAsync()
        {
            var rosterKey = "Settings_RosterList";
            if (AppSettings.Values.TryGetValue(rosterKey, out var rosterObj))
            {
                try
                {
                    var jsonStr = rosterObj.ToString();
                    if (jsonStr != null)
                    {
                        var students = JsonSerializer.Deserialize<string[]>(jsonStr);
                        if (students != null)
                        {
                            _studentNames = students.ToList();
                        }
                    }
                }
                catch { }
            }

            if (_studentNames.Count == 0)
            {
                var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
                if (dataProvider == "本地存储")
                {
                    var localResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Get, "/kv/classworks-list-main", null);
                    if (localResponse.IsSuccessStatusCode)
                    {
                        var body = await localResponse.Content.ReadAsStringAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
                            {
                                var data = dataEl.GetString();
                                if (!string.IsNullOrEmpty(data))
                                {
                                    var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    _studentNames = lines.Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                                }
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    var token = AppSettings.Values["Token"] as string;
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        var baseUrl = (AppSettings.Values["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev").TrimEnd('/');
                        try
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/kv/{Uri.EscapeDataString("classworks-list-main")}");
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
                            using var response = await _httpClient.SendAsync(request);
                            if (response.IsSuccessStatusCode)
                            {
                                var body = await response.Content.ReadAsStringAsync();
                                using var doc = JsonDocument.Parse(body);
                                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
                                {
                                    var data = dataEl.GetString();
                                    if (!string.IsNullOrEmpty(data))
                                    {
                                        var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                        _studentNames = lines.Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            UpdateAvailableCount();
        }

        private void UpdateAvailableCount()
        {
            if (_isNameMode)
            {
                _availableStudents = _studentNames.ToList();
                _availableCountText.Text = $"当前可抽取学生: {_availableStudents.Count}人";
            }
            else
            {
                var min = RandomPickerSettings.MinNumber;
                var max = RandomPickerSettings.MaxNumber;
                var range = Math.Max(0, max - min + 1);
                _availableCountText.Text = $"当前可抽取学号: {range}个（{min}~{max}）";
            }
        }

        private async void StartPick_Click(object sender, RoutedEventArgs e)
        {
            int maxAvailable;
            if (_isNameMode)
            {
                if (_studentNames.Count == 0)
                {
                    await ShowDialogAsync("没有可抽取的学生，请先添加学生名单。");
                    return;
                }
                _availableStudents = _studentNames.ToList();
                maxAvailable = _availableStudents.Count;
            }
            else
            {
                var min = RandomPickerSettings.MinNumber;
                var max = RandomPickerSettings.MaxNumber;
                maxAvailable = Math.Max(0, max - min + 1);
                if (maxAvailable == 0)
                {
                    await ShowDialogAsync("学号范围设置无效，请在设置中调整。");
                    return;
                }
            }

            var count = Math.Min(_pickCount, maxAvailable);
            if (count == 0)
            {
                await ShowDialogAsync("没有可抽取的对象。");
                return;
            }

            if (RandomPickerSettings.AnimationEnabled)
            {
                await RunAnimationAsync(count);
            }
            else
            {
                _animationOverlay.Visibility = Visibility.Visible;
                ShowResults(count);
            }
        }

        private async Task RunAnimationAsync(int count)
        {
            _animationOverlay.Visibility = Visibility.Visible;
            _dismissHintText.Visibility = Visibility.Collapsed;
            _animationText.Visibility = Visibility.Visible;
            _resultText.Visibility = Visibility.Collapsed;
            _resultListPanel.Visibility = Visibility.Collapsed;
            _btnStartPick.IsEnabled = false;

            var minNum = RandomPickerSettings.MinNumber;
            var maxNum = RandomPickerSettings.MaxNumber;

            var duration = 1500;
            var interval = 50;
            var elapsed = 0;

            while (elapsed < duration)
            {
                string displayItem;
                if (_isNameMode)
                {
                    displayItem = _availableStudents[_random.Next(_availableStudents.Count)];
                }
                else
                {
                    displayItem = _random.Next(minNum, maxNum + 1).ToString();
                }
                _animationText.Text = displayItem;
                _animationText.FontSize = 72;
                await Task.Delay(interval);
                elapsed += interval;
                if (interval < 200)
                    interval += 5;
            }

            ShowResults(count);
        }

        private void ShowResults(int count)
        {
            var picked = new List<string>();

            if (_isNameMode)
            {
                var pool = _availableStudents.ToList();
                for (int i = 0; i < count && pool.Count > 0; i++)
                {
                    var idx = _random.Next(pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
            }
            else
            {
                var minNum = RandomPickerSettings.MinNumber;
                var maxNum = RandomPickerSettings.MaxNumber;
                var used = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    int num;
                    do
                    {
                        num = _random.Next(minNum, maxNum + 1);
                    } while (used.Contains(num));
                    used.Add(num);
                    picked.Add(num.ToString());
                }
            }

            _animationText.Visibility = Visibility.Collapsed;
            _resultText.Visibility = Visibility.Collapsed;
            _resultListPanel.Children.Clear();
            _resultListPanel.Visibility = Visibility.Collapsed;

            if (picked.Count == 1)
            {
                _resultText.Text = picked[0];
                _resultText.FontSize = 72;
            }
            else
            {
                _resultText.Text = $"共抽取 {picked.Count} 个";
                _resultText.FontSize = 32;

                _resultListPanel.Visibility = Visibility.Visible;
                foreach (var item in picked)
                {
                    var nameCard = new Border
                    {
                        Background = _accentBrush,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(16, 10, 16, 10),
                        MinWidth = 120,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Opacity = 0,
                        RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = 15 },
                        Child = new TextBlock
                        {
                            Text = item,
                            FontSize = 28,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = _accentForeground,
                            TextAlignment = TextAlignment.Center
                        }
                    };
                    _resultListPanel.Children.Add(nameCard);
                }
            }

            _resultText.Visibility = Visibility.Visible;
            _resultText.Opacity = 0;
            _resultText.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = 20 };

            var sb = new Storyboard();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeIn, _resultText);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            sb.Children.Add(fadeIn);

            var slideUp = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(slideUp, _resultText.RenderTransform);
            Storyboard.SetTargetProperty(slideUp, "Y");
            sb.Children.Add(slideUp);

            for (int i = 0; i < _resultListPanel.Children.Count; i++)
            {
                var child = _resultListPanel.Children[i];
                var delay = 100 + i * 80;

                var cardFadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    BeginTime = TimeSpan.FromMilliseconds(delay),
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(cardFadeIn, child);
                Storyboard.SetTargetProperty(cardFadeIn, "Opacity");
                sb.Children.Add(cardFadeIn);

                var cardSlideUp = new DoubleAnimation
                {
                    From = 15,
                    To = 0,
                    BeginTime = TimeSpan.FromMilliseconds(delay),
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(cardSlideUp, child.RenderTransform);
                Storyboard.SetTargetProperty(cardSlideUp, "Y");
                sb.Children.Add(cardSlideUp);
            }

            sb.Begin();

            _dismissHintText.Visibility = Visibility.Visible;
        }

        private async Task ShowDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "提示",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
