using CSD.Settings;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Net.Http;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;




namespace CSD.Views
{
    /// <summary>
    /// 设置窗口，用于管理和展示应用配置项。
    /// 现已通过 Settings 文件夹下的各个模块进行了模块化重构。
    /// </summary>
    public class SettingsWindow : Window
    {
        private sealed class NavigationItemState
        {
            public required string CategoryKey { get; init; }
            public required Button Button { get; init; }
            public required Border HoverBackground { get; init; }
            public required TextBlock Label { get; init; }
            public required SolidColorBrush IconBrush { get; init; }
            public required SolidColorBrush LabelBrush { get; init; }
        }

        private readonly Action _onSettingsChanged;
        private readonly HttpClient _settingsHttpClient = new();
        private readonly TextBlock _pageTitleText;
        private readonly TextBlock _pageDescriptionText;
        private readonly Grid _detailsHost;
        private readonly List<Button> _navigationButtons = new();
        private readonly Dictionary<string, NavigationItemState> _navigationItemStates = new();
        private readonly Dictionary<string, FrameworkElement> _categoryViews = new();
        private readonly Border _selectionHighlight;
        private readonly Grid _navigationItemsHost;
        private readonly StackPanel _navigationItemsPanel;
        private readonly ScrollViewer _navigationScrollViewer;
        private readonly Grid _appTitleBar;
        private readonly ColumnDefinition _leftInsetColumn;
        private readonly ColumnDefinition _rightInsetColumn;
        private string _activeCategoryKey = "server";

        private readonly List<ISettingsModule> _modules = new();
        private readonly SettingsContext _settingsContext;

        public SettingsWindow(Action onSettingsChanged)
        {
            _onSettingsChanged = () => {
                onSettingsChanged();
                VisualHelper.ApplyWindowBackdrop(this);
            };
            _settingsContext = new SettingsContext(this, _settingsHttpClient);

            Title = "设置";
            VisualHelper.ApplyWindowBackdrop(this);

            _pageTitleText = new TextBlock
            {
                FontSize = 30,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            _pageDescriptionText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            _detailsHost = new Grid
            {
                MaxWidth = 920
            };

            _selectionHighlight = new Border
            {
                Height = 46,
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(SettingsUIHelper.WithAlpha(SettingsUIHelper.GetBrushColor("AccentFillColorDefaultBrush", Colors.DodgerBlue), 40)),
                Opacity = 1,
                IsHitTestVisible = false
            };

            _navigationItemsHost = new Grid();
            _navigationItemsPanel = new StackPanel { Spacing = 8 };
            _navigationItemsHost.Children.Add(_selectionHighlight);
            _navigationItemsHost.Children.Add(_navigationItemsPanel);

            var contentStack = new StackPanel
            {
                Spacing = 20,
                Padding = new Thickness(28, 24, 28, 32)
            };
            contentStack.Children.Add(_pageTitleText);
            contentStack.Children.Add(_pageDescriptionText);
            contentStack.Children.Add(_detailsHost);

            var contentScrollViewer = new ScrollViewer
            {
                Content = contentStack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var sidebar = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(12)
            };

            var navigationStack = new StackPanel { Spacing = 8 };
            navigationStack.Children.Add(new TextBlock
            {
                Text = "设置",
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(8, 4, 8, 12)
            });

            _modules.Add(new ServerSettingsModule());
            _modules.Add(new SubjectSettingsModule());
            _modules.Add(new RosterSettingsModule());
            _modules.Add(new RefreshSettingsModule());
            _modules.Add(new NotificationSettingsModule());
            _modules.Add(new EditSettingsModule());
            _modules.Add(new DisplaySettingsModule());
            _modules.Add(new PlaybackSettingsModule());
            _modules.Add(new RandomPickerSettingsModule());
            _modules.Add(new PerformanceSettingsModule());
            _modules.Add(new AutoStartSettingsModule());
            _modules.Add(new CloseBehaviorSettingsModule());
            _modules.Add(new AccountSettingsModule());
            _modules.Add(new UpdateSettingsModule());

            foreach (var module in _modules)
            {
                module.Initialize(_settingsContext);
                module.SettingsChanged += _onSettingsChanged;
                
                var view = module.CreateView();
                _categoryViews[module.CategoryKey] = view;
                _detailsHost.Children.Add(view);

                var btn = CreateNavigationButton(module.Title, module.CategoryKey, string.IsNullOrEmpty(module.ImageIconUri) ? module.Glyph : null, string.IsNullOrEmpty(module.ImageIconUri) ? null : module.ImageIconUri);
                _navigationItemsPanel.Children.Add(btn);
            }

            navigationStack.Children.Add(_navigationItemsHost);

            _navigationScrollViewer = new ScrollViewer
            {
                Content = navigationStack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            sidebar.Child = _navigationScrollViewer;

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
                HorizontalAlignment = HorizontalAlignment.Left,
                Spacing = 0
            };
            titleBarTextStack.Children.Add(new TextBlock
            {
                Text = "CSD",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            titleBarTextStack.Children.Add(new TextBlock
            {
                Text = "设置",
                Margin = new Thickness(0, -1, 0, 0),
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            Grid.SetColumn(titleBarTextStack, 1);
            _appTitleBar.Children.Add(titleBarTextStack);

            var contentRoot = new Grid
            {
                Padding = new Thickness(20),
                ColumnSpacing = 20
            };
            contentRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            contentRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(sidebar, 0);
            Grid.SetColumn(contentScrollViewer, 1);
            contentRoot.Children.Add(sidebar);
            contentRoot.Children.Add(contentScrollViewer);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_appTitleBar, 0);
            Grid.SetRow(contentRoot, 1);
            root.Children.Add(_appTitleBar);
            root.Children.Add(contentRoot);

            Content = root;
            ConfigureIntegratedTitleBar();
            ShowCategory("server");
            UpdateNavigationSelection("server", animateHighlight: false);

            root.Loaded += (_, _) =>
            {
                AnimationHelper.AnimateEntrance(root, fromY: 18f, durationMs: 360);
                AnimationHelper.ApplyStandardInteractions(contentScrollViewer);
                UpdateNavigationSelection(_activeCategoryKey, animateHighlight: false);
            };
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

        private void ShowCategory(string categoryKey)
        {
            foreach (var categoryView in _categoryViews)
            {
                categoryView.Value.Visibility = string.Equals(categoryView.Key, categoryKey, StringComparison.Ordinal)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            ISettingsModule? activeModule = null;
            foreach (var module in _modules)
            {
                if (string.Equals(module.CategoryKey, categoryKey, StringComparison.Ordinal))
                {
                    activeModule = module;
                    break;
                }
            }

            if (activeModule != null)
            {
                _pageTitleText.Text = activeModule.Title;
                _pageTitleText.Visibility = string.IsNullOrEmpty(activeModule.Title) ? Visibility.Collapsed : Visibility.Visible;
                
                _pageDescriptionText.Text = activeModule.Description;
                _pageDescriptionText.Visibility = string.IsNullOrEmpty(activeModule.Description) ? Visibility.Collapsed : Visibility.Visible;

                activeModule.OnNavigatedTo();
            }
        }

        private Button CreateNavigationButton(string title, string tag, string? glyph = null, string? imageIconUri = null)
        {
            var hoverBackground = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(SettingsUIHelper.WithAlpha(SettingsUIHelper.GetBrushColor("TextFillColorSecondaryBrush", Colors.LightGray), 26)),
                Opacity = 0,
                IsHitTestVisible = false
            };

            var iconBrush = new SolidColorBrush(SettingsUIHelper.GetBrushColor("TextFillColorSecondaryBrush", Colors.LightGray));
            var labelBrush = new SolidColorBrush(SettingsUIHelper.GetBrushColor("TextFillColorPrimaryBrush", Colors.White));
            var label = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = labelBrush
            };

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(label, 1);
            label.Margin = new Thickness(12, 0, 0, 0);
            contentGrid.Children.Add(hoverBackground);
            FrameworkElement iconElement;
            if (!string.IsNullOrWhiteSpace(imageIconUri))
            {
                iconElement = new Image
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(imageIconUri)),
                    Width = 18,
                    Height = 18,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                iconElement = new FontIcon
                {
                    Glyph = glyph ?? "",
                    FontSize = 16,
                    Foreground = iconBrush
                };
            }
            Grid.SetColumn(iconElement, 0);
            contentGrid.Children.Add(iconElement);
            contentGrid.Children.Add(label);
            Grid.SetColumnSpan(hoverBackground, 2);

            var button = new Button
            {
                Content = contentGrid,
                Tag = tag,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 12, 14, 12),
                MinHeight = 46,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(12)
            };
            button.Click += NavigationButton_Click;
            button.PointerEntered += NavigationButton_PointerEntered;
            button.PointerExited += NavigationButton_PointerExited;
            button.PointerCanceled += NavigationButton_PointerExited;
            button.PointerCaptureLost += NavigationButton_PointerExited;
            button.Loaded += NavigationButton_Loaded;
            button.SizeChanged += NavigationButton_SizeChanged;

            _navigationButtons.Add(button);
            _navigationItemStates[tag] = new NavigationItemState
            {
                CategoryKey = tag,
                Button = button,
                HoverBackground = hoverBackground,
                Label = label,
                IconBrush = iconBrush,
                LabelBrush = labelBrush
            };
            return button;
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryKey)
            {
                ShowCategory(categoryKey);
                UpdateNavigationSelection(categoryKey, animateHighlight: true);
            }
        }

        private void NavigationButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryKey &&
                _navigationItemStates.TryGetValue(categoryKey, out var state) &&
                !string.Equals(categoryKey, _activeCategoryKey, StringComparison.Ordinal))
            {
                AnimationHelper.AnimateToOpacity(state.HoverBackground, 1f, 180);
            }
        }

        private void NavigationButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryKey &&
                _navigationItemStates.TryGetValue(categoryKey, out var state) &&
                !string.Equals(categoryKey, _activeCategoryKey, StringComparison.Ordinal))
            {
                AnimationHelper.AnimateToOpacity(state.HoverBackground, 0f, 180);
            }
        }

        private void NavigationButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && string.Equals(button.Tag as string, _activeCategoryKey, StringComparison.Ordinal))
            {
                UpdateNavigationSelection(_activeCategoryKey, animateHighlight: false);
            }
        }

        private void NavigationButton_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Button button && string.Equals(button.Tag as string, _activeCategoryKey, StringComparison.Ordinal))
            {
                UpdateNavigationSelection(_activeCategoryKey, animateHighlight: false);
            }
        }

        private void UpdateNavigationSelection(string activeCategoryKey, bool animateHighlight)
        {
            _activeCategoryKey = activeCategoryKey;
            var primaryTextColor = SettingsUIHelper.GetBrushColor("TextFillColorPrimaryBrush", Colors.White);
            var secondaryTextColor = SettingsUIHelper.GetBrushColor("TextFillColorSecondaryBrush", Colors.LightGray);
            var accentColor = SettingsUIHelper.GetBrushColor("AccentTextFillColorPrimaryBrush", SettingsUIHelper.GetBrushColor("AccentFillColorDefaultBrush", Colors.DodgerBlue));

            foreach (var button in _navigationButtons)
            {
                bool isActive = string.Equals(button.Tag as string, activeCategoryKey, StringComparison.Ordinal);
                if (button.Tag is string categoryKey && _navigationItemStates.TryGetValue(categoryKey, out var state))
                {
                    AnimationHelper.AnimateToOpacity(state.HoverBackground, 0f, 140);

                    var iconColor = isActive ? accentColor : secondaryTextColor;
                    var labelColor = isActive ? primaryTextColor : secondaryTextColor;

                    // When the visual tree is not ready (e.g. during construction before Activate()),
                    // set colors directly to avoid Storyboard crashes in AnimateBrushColor.
                    if (button.XamlRoot is not null)
                    {
                        AnimationHelper.AnimateBrushColor(state.IconBrush, iconColor, 220);
                        AnimationHelper.AnimateBrushColor(state.LabelBrush, labelColor, 220);
                    }
                    else
                    {
                        state.IconBrush.Color = iconColor;
                        state.LabelBrush.Color = labelColor;
                    }
                }
            }

            if (_navigationItemStates.TryGetValue(activeCategoryKey, out var activeState) && activeState.Button.ActualHeight > 0)
            {
                var transform = activeState.Button.TransformToVisual(_navigationItemsHost);
                var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                _selectionHighlight.Height = activeState.Button.ActualHeight;
                if (animateHighlight)
                {
                    AnimationHelper.AnimateOffsetY(_selectionHighlight, (float)point.Y, 260, 5f);
                    AnimationHelper.AnimateScaleTo(_selectionHighlight, 1f, 260, 0.025f);
                }
                else
                {
                    try
                    {
                        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_selectionHighlight);
                        visual.Offset = new System.Numerics.Vector3(0, (float)point.Y, 0);
                        visual.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
                    }
                    catch
                    {
                        // Element not yet in visual tree — will be positioned on Loaded
                    }
                }
            }
        }
    }
}