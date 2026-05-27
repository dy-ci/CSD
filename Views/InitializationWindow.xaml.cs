using Microsoft.UI.Windowing;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;

using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;




namespace CSD.Views
{
    /// <summary>
    /// Initialization window shown when the app starts.
    /// </summary>
    public sealed partial class InitializationWindow : Window
    {
        private const string TokenSettingsKey = "Token";
        private const string IntroWord = "Classworks";

        private readonly List<Border> _introBlocks = new();
        private readonly List<TextBlock> _introTexts = new();
        private readonly List<TranslateTransform> _introBlockTranslations = new();
        private readonly List<ScaleTransform> _introBlockScales = new();
        private readonly List<PlaneProjection> _introBlockProjections = new();
        private readonly List<PlaneProjection> _introTextProjections = new();
        private Grid? _animationStage;
        private Grid? _contentRoot;
        private Grid? _introOverlay;
        private Image? _welcomeLogo;
        private StackPanel? _welcomeActionsPanel;
        private StackPanel? _contentTextHost;
        private StackPanel? _introTextPanel;
        private TranslateTransform? _introTextPanelTranslation;
        private TextBlock? _introDesktopText;
        private TranslateTransform? _introDesktopTextTranslation;
        private ScaleTransform? _introDesktopTextScale;
        private Button? _nextButton;

        private bool _hasPlayedInitializationAnimation;
        private bool _isTransitioningToForm;
        private bool _hasAppliedOobeInteractions;
        private bool _hasShownOptionsHeader;

        public InitializationWindow()
        {
            InitializeComponent();
            TouchKeyboardHelper.EnableForControl(TokenBox);
            TouchKeyboardHelper.EnableForControl(NamespaceBox);
            TouchKeyboardHelper.EnableForControl(AuthPasswordBox);
            BuildAnimationVisuals();
            ConfigureIntegratedTitleBar();
            VisualHelper.ApplyWindowBackdrop(this);
            SetChoiceIcons();

            try
            {
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch { }

            RestoreWindowState();
            Closed += (sender, args) => SaveWindowState();

            if (Content is FrameworkElement rootContent)
            {
                rootContent.Loaded += RootContent_Loaded;
            }
        }

        private void SetChoiceIcons()
        {
            CloudSyncIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(AppSettings.GetAssetUri("icons/ic_gallery_cloud_synchronization.ico")) { DecodePixelWidth = 36, DecodePixelHeight = 36 };
            LocalOnlyIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(AppSettings.GetAssetUri("icons/ic_device_matebook.ico")) { DecodePixelWidth = 36, DecodePixelHeight = 36 };
        }

        private void ConfigureIntegratedTitleBar()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            UpdateTitleBarLayout(AppWindow.TitleBar);
        }

        private void UpdateTitleBarLayout(AppWindowTitleBar titleBar)
        {
            LeftInsetColumn.Width = new GridLength(titleBar.LeftInset);
            RightInsetColumn.Width = new GridLength(titleBar.RightInset);
        }

        private void RootContent_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement rootContent)
            {
                rootContent.Loaded -= RootContent_Loaded;
                _ = RunInitializationSequenceAsync();
            }
        }

        private void BuildAnimationVisuals()
        {
            FormScrollViewer.Visibility = Visibility.Collapsed;
            FormPanel.Opacity = 0;
            FormPanel.IsHitTestVisible = false;
            FormScrollViewer.IsHitTestVisible = false;
            FormPanel.RenderTransform = new TranslateTransform { Y = 20 };

            _animationStage = new Grid
            {
                IsHitTestVisible = false
            };

            _contentRoot = new Grid
            {
                Opacity = 0,
                RowDefinitions =
                {
                    new RowDefinition(),
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition()
                }
            };

            _welcomeLogo = new Image
            {
                Width = 80,
                Height = 80,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 8),
                Opacity = 0,
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(AppSettings.GetAssetUri("icons/Classworks.ico")) { DecodePixelWidth = 80, DecodePixelHeight = 80 }
            };
            Grid.SetRow(_welcomeLogo, 0);
            _contentRoot.Children.Add(_welcomeLogo);

            _contentTextHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            };
            for (int i = 0; i < IntroWord.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text = IntroWord[i].ToString(),
                    FontSize = 32,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center
                };
                _contentTextHost.Children.Add(tb);
            }
            Grid.SetRow(_contentTextHost, 1);
            _contentRoot.Children.Add(_contentTextHost);

            _welcomeActionsPanel = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0
            };
            _introOverlay = new Grid
            {
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };
            _introOverlay.RenderTransform = new ScaleTransform { ScaleX = 1.25, ScaleY = 1.25 };

            var introGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            BuildIntroVisualTree(introGrid);
            _introOverlay.Children.Add(introGrid);

            _nextButton = new Button
            {
                Content = new SymbolIcon(Symbol.Forward),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Margin = new Thickness(0, 8, 0, 0)
            };
            _nextButton.Click += NextButton_Click;
            _welcomeActionsPanel.Children.Add(_nextButton);
            Grid.SetRow(_welcomeActionsPanel, 2);
            _contentRoot.Children.Add(_welcomeActionsPanel);

            _animationStage.Children.Add(_contentRoot);
            _animationStage.Children.Add(_introOverlay);
            ContentHost.Children.Add(_animationStage);
        }

        private void BuildIntroVisualTree(Grid host)
        {
            _introBlocks.Clear();
            _introTexts.Clear();
            _introBlockTranslations.Clear();
            _introBlockScales.Clear();
            _introBlockProjections.Clear();
            _introTextProjections.Clear();

            var rects = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4
            };
            _introTextPanelTranslation = new TranslateTransform();
            _introTextPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4,
                RenderTransform = _introTextPanelTranslation
            };

            for (int i = 0; i < IntroWord.Length; i++)
            {
                var translateTransform = new TranslateTransform { Y = 50 };
                var scaleTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
                var blockTransforms = new TransformGroup();
                blockTransforms.Children.Add(translateTransform);
                blockTransforms.Children.Add(scaleTransform);
                var blockProjection = new PlaneProjection { RotationX = 0 };

                var block = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(i == 5
                        ? Color.FromArgb(255, 0, 191, 255)
                        : Color.FromArgb(255, 242, 242, 242)),
                    Opacity = 0,
                    RenderTransformOrigin = new Windows.Foundation.Point(0, 12),
                    RenderTransform = blockTransforms,
                    Projection = blockProjection
                };
                if (i == 5)
                {
                    Canvas.SetZIndex(block, 1);
                }

                var textProjection = new PlaneProjection { RotationX = -90 };
                var text = new TextBlock
                {
                    Text = IntroWord[i].ToString(),
                    MinWidth = 32,
                    FontSize = 32,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    Opacity = 0,
                    RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                    Projection = textProjection
                };

                rects.Children.Add(block);
                _introTextPanel.Children.Add(text);

                _introBlocks.Add(block);
                _introTexts.Add(text);
                _introBlockTranslations.Add(translateTransform);
                _introBlockScales.Add(scaleTransform);
                _introBlockProjections.Add(blockProjection);
                _introTextProjections.Add(textProjection);
            }

            _introDesktopTextTranslation = new TranslateTransform();
            _introDesktopTextScale = new ScaleTransform { ScaleX = 0.965, ScaleY = 0.965 };
            var introDesktopTextTransforms = new TransformGroup();
            introDesktopTextTransforms.Children.Add(_introDesktopTextScale);
            introDesktopTextTransforms.Children.Add(_introDesktopTextTranslation);
            _introDesktopText = new TextBlock
            {
                Text = "Desktop",
                FontSize = 32,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = introDesktopTextTransforms,
                Foreground = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Color.FromArgb(255, 0x00, 0xCC, 0xFF), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(255, 0x00, 0x7F, 0xFF), Offset = 1 }
                    }
                }
            };

            host.Children.Add(rects);
            host.Children.Add(_introTextPanel);
            host.Children.Add(_introDesktopText);
        }

        private void ResetIntroVisualState()
        {
            if (FormPanel != null)
            {
                FormPanel.Opacity = 0;
                if (FormPanel.RenderTransform is TranslateTransform formTransform)
                {
                    formTransform.Y = 20;
                }
            }

            if (_introOverlay?.RenderTransform is ScaleTransform overlayScale)
            {
                overlayScale.ScaleX = 1.25;
                overlayScale.ScaleY = 1.25;
            }

            if (_introOverlay != null)
            {
                _introOverlay.Opacity = 0;
            }

            if (_contentRoot != null)
            {
                _contentRoot.Opacity = 0;
            }
            if (_welcomeLogo != null)
            {
                _welcomeLogo.Opacity = 0;
            }
            if (_welcomeActionsPanel != null)
            {
                _welcomeActionsPanel.Opacity = 0;
            }
            if (_contentTextHost != null)
            {
                _contentTextHost.Opacity = 0;
            }
            if (_introTextPanelTranslation != null)
            {
                _introTextPanelTranslation.X = 0;
            }
            if (_introDesktopText != null)
            {
                _introDesktopText.Opacity = 0;
            }
            if (_introDesktopTextTranslation != null)
            {
                _introDesktopTextTranslation.X = 0;
            }
            if (_introDesktopTextScale != null)
            {
                _introDesktopTextScale.ScaleX = 0.965;
                _introDesktopTextScale.ScaleY = 0.965;
            }
            if (_nextButton != null)
            {
                _nextButton.IsEnabled = false;
            }

            for (int i = 0; i < _introBlocks.Count; i++)
            {
                _introBlocks[i].Opacity = 0;
                _introBlockTranslations[i].Y = 50;
                _introBlockScales[i].ScaleX = 1;
                _introBlockScales[i].ScaleY = 1;
                _introBlockProjections[i].RotationX = 0;
                _introTexts[i].Opacity = 0;
                _introTexts[i].MinWidth = 32;
                _introTextProjections[i].RotationX = -90;
            }
        }

        private async Task RunInitializationSequenceAsync()
        {
            if (_hasPlayedInitializationAnimation)
            {
                return;
            }

            _hasPlayedInitializationAnimation = true;
            ResetIntroVisualState();

            await Task.Delay(80);
            await PlayOobeIntroAnimationAsync();
        }

        private async Task PlayOobeIntroAnimationAsync()
        {
            if (_introOverlay == null)
            {
                return;
            }

            TitleStatusText.Text = "欢迎";

            if (_introOverlay.RenderTransform is ScaleTransform overlayScale)
            {
                StartDoubleAnimation(overlayScale, nameof(ScaleTransform.ScaleX), 1.25, 1, 3000, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
                StartDoubleAnimation(overlayScale, nameof(ScaleTransform.ScaleY), 1.25, 1, 3000, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            StartDoubleAnimation(_introOverlay, nameof(UIElement.Opacity), 0, 1, 3000, 0, new CubicEase { EasingMode = EasingMode.EaseOut });

            double elapsedDelayMs = 0;
            double maxEndMs = 0;
            int count = _introBlocks.Count;
            const double durationMs = 500;

            for (int i = 0; i < count; i++)
            {
                var stepDelay = Math.Sin(((i + 2d) / (count + 2d)) * (Math.PI / 2d)) * durationMs / count;
                var phaseMs = stepDelay * 9;
                StartIntroLetterAnimation(i, phaseMs);
                maxEndMs = Math.Max(maxEndMs, elapsedDelayMs + (phaseMs * 2) + 750);
                elapsedDelayMs += stepDelay;
                await Task.Delay(TimeSpan.FromMilliseconds(stepDelay));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, maxEndMs - elapsedDelayMs)));
            await RevealWelcomeChromeAsync();
        }

        private void StartIntroLetterAnimation(int index, double phaseMs)
        {
            var easeOut = new ExponentialEase { EasingMode = EasingMode.EaseOut };
            var easeIn = new ExponentialEase { EasingMode = EasingMode.EaseIn };

            StartDoubleAnimation(_introBlockTranslations[index], nameof(TranslateTransform.Y), 50, 0, phaseMs, 0, easeOut);
            StartDoubleAnimation(_introBlocks[index], nameof(UIElement.Opacity), 0, 1, phaseMs, 0, easeOut);

            if (index == 5)
            {
                StartDoubleAnimation(_introBlockScales[index], nameof(ScaleTransform.ScaleX), 1, 2.17, 250, 307, new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            if (index == 6)
            {
                StartDoubleAnimation(_introBlocks[index], nameof(UIElement.Opacity), 1, 0, 1, phaseMs + 1, easeOut);
            }

            StartDoubleAnimation(_introBlockProjections[index], nameof(PlaneProjection.RotationX), 0, 90, phaseMs * 0.5, phaseMs + 750, easeIn);
            StartDoubleAnimation(_introBlocks[index], nameof(UIElement.Opacity), index == 6 ? 0 : 1, 0, phaseMs * 0.5, phaseMs + 750, easeIn);

            StartDoubleAnimation(_introTextProjections[index], nameof(PlaneProjection.RotationX), -90, 0, phaseMs * 0.5, (phaseMs * 1.5) + 750, easeOut);
            StartDoubleAnimation(_introTexts[index], nameof(UIElement.Opacity), 0, 1, phaseMs * 0.5, (phaseMs * 1.5) + 750, easeOut);
        }

        private async Task RevealWelcomeChromeAsync()
        {
            if (_contentRoot == null || _introOverlay == null || _nextButton == null || _welcomeLogo == null || _welcomeActionsPanel == null)
            {
                return;
            }

            TitleStatusText.Text = "欢迎";

            _contentRoot.Opacity = 1;
            StartDoubleAnimation(_welcomeLogo, nameof(UIElement.Opacity), 0, 1, 200, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
            StartDoubleAnimation(_welcomeActionsPanel, nameof(UIElement.Opacity), 0, 1, 200, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
            await Task.Delay(20);

            foreach (var tb in _introTexts)
            {
                StartDoubleAnimation(tb, nameof(FrameworkElement.MinWidth), 32, 0, 700, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
            }
            if (_introTextPanel != null)
            {
                StartDoubleAnimation(_introTextPanel, nameof(StackPanel.Spacing), 4, 0, 700, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
            }

            await Task.Delay(720);

            if (_introDesktopText != null && _introDesktopTextTranslation != null && _introTextPanel != null)
            {
                _introOverlay.UpdateLayout();
                _introTextPanel.UpdateLayout();
                _introDesktopText.UpdateLayout();

                const double gap = 10;
                double desktopWidth = _introDesktopText.ActualWidth;
                double classworksTargetX = -((gap + desktopWidth) / 2d);
                double desktopTargetX = (_introTextPanel.ActualWidth / 2d) + (gap / 2d);

                if (_introTextPanelTranslation != null)
                {
                    StartDoubleAnimation(_introTextPanelTranslation, nameof(TranslateTransform.X), 0, classworksTargetX, 300, 0, new ExponentialEase { Exponent = 5, EasingMode = EasingMode.EaseOut });
                }

                _introDesktopTextTranslation.X = desktopTargetX + 14;
                StartDoubleAnimation(_introDesktopTextTranslation, nameof(TranslateTransform.X), desktopTargetX + 14, desktopTargetX, 220, 20, new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut });
                StartDoubleAnimation(_introDesktopText, nameof(UIElement.Opacity), 0, 1, 180, 35, new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut });
                if (_introDesktopTextScale != null)
                {
                    StartDoubleAnimation(_introDesktopTextScale, nameof(ScaleTransform.ScaleX), 0.965, 1, 220, 20, new QuadraticEase { EasingMode = EasingMode.EaseOut });
                    StartDoubleAnimation(_introDesktopTextScale, nameof(ScaleTransform.ScaleY), 0.965, 1, 220, 20, new QuadraticEase { EasingMode = EasingMode.EaseOut });
                }
            }

            await Task.Delay(320);
            _nextButton.IsEnabled = true;
            _animationStage!.IsHitTestVisible = true;
        }

        private async Task ShowFormAsync()
        {
            if (_isTransitioningToForm)
            {
                return;
            }
            _isTransitioningToForm = true;

            if (_animationStage != null)
            {
                await FadeVisualOpacityAsync(_animationStage, 1f, 0f, 220);
                _animationStage.Visibility = Visibility.Collapsed;
                _animationStage.IsHitTestVisible = false;
            }

            TitleStatusText.Text = "欢迎使用 Classworks";
            FormScrollViewer.Visibility = Visibility.Visible;
            FormPanel.IsHitTestVisible = true;
            FormScrollViewer.IsHitTestVisible = true;

            if (!_hasAppliedOobeInteractions)
            {
                AnimationHelper.ApplyStandardInteractions(FormPanel);
                _hasAppliedOobeInteractions = true;
            }

            StartDoubleAnimation(FormPanel, nameof(UIElement.Opacity), 0, 1, 400, 0, new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut });
            if (FormPanel.RenderTransform is TranslateTransform transform)
            {
                StartDoubleAnimation(transform, nameof(TranslateTransform.Y), 20, 0, 400, 0, new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut });
            }

            ShowOptionsPanel(animate: true);
        }

        private void HideAllOobePanels()
        {
            OptionsPanel.Visibility = Visibility.Collapsed;
            CloudChoicePanel.Visibility = Visibility.Collapsed;
            TokenInputPanel.Visibility = Visibility.Collapsed;
            DeviceAuthPanel.Visibility = Visibility.Collapsed;
            PerformancePanel.Visibility = Visibility.Collapsed;
        }

        private async void ShowPerformancePanel(bool animate = true)
        {
            ResetOobeViewport();
            WelcomeText.Visibility = Visibility.Collapsed;
            InstructionText.Visibility = Visibility.Collapsed;
            HideAllOobePanels();
            PerformancePanel.Visibility = Visibility.Visible;

            if (animate)
            {
                AnimateOobePanel(PerformancePanel);
            }

            // Start detection
            InitScoreText.Text = "--";
            InitRatingText.Text = "正在评估...";
            
            var info = await Task.Run(() => PerformanceService.GetPerformanceInfo());

            InitScoreText.Text = info.Score.ToString();
            InitRatingText.Text = info.Rating;
            InitRatingDescText.Text = info.RatingDescription;

            if (info.Cpus.Count > 0) InitCpuText.Text = info.Cpus[0].Model;
            if (info.Gpus.Count > 0) InitGpuText.Text = info.Gpus[0].Name;

            // Apply smart presets and theme-aware color
            var presets = PerformanceFormatter.GetOptimizationPresets(info.Score);
            InitPageAnimCheck.IsChecked = presets.PageAnim;
            InitInterAnimCheck.IsChecked = presets.InterAnim;
            InitBlurCheck.IsChecked = presets.Blur;
            InitHighRateCheck.IsChecked = presets.HighRate;
            InitHighResCheck.IsChecked = presets.HighRes;
            InitScoreText.Foreground = PerformanceFormatter.GetScoreBrush(info.Score);
        }

        private void PerformanceFinishButton_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Values["Settings_PageTransitionAnimations"] = InitPageAnimCheck.IsChecked == true;
            AppSettings.Values["Settings_ElementInteractionAnimations"] = InitInterAnimCheck.IsChecked == true;
            AppSettings.Values["Settings_BackgroundBlurEffects"] = InitBlurCheck.IsChecked == true;
            AppSettings.Values["Settings_HighFramerateRendering"] = InitHighRateCheck.IsChecked == true;
            AppSettings.Values["Settings_HighResResourceLoading"] = InitHighResCheck.IsChecked == true;

            var mainWindow = new MainWindow();
            mainWindow.Activate();
            Close();
        }

        private void FinalizeLogin(string token, string? provider = "Classworks 云端存储")
        {
            AppSettings.Values[TokenSettingsKey] = token;
            if (provider != null) AppSettings.Values["Settings_DataProvider"] = provider;
            
            ShowPerformancePanel();
        }

        private void ResetOobeViewport()
        {
            FormScrollViewer.ChangeView(null, 0, null, true);
        }

        private void AnimateOobePanel(Panel panel, bool includeHeader = false)
        {
            double delay = 0;

            if (includeHeader && WelcomeText.Visibility == Visibility.Visible)
            {
                AnimationHelper.AnimateEntrance(WelcomeText, fromY: 12f, durationMs: 260, delayMs: delay);
                delay += 35;
            }

            if (includeHeader && InstructionText.Visibility == Visibility.Visible)
            {
                AnimationHelper.AnimateEntrance(InstructionText, fromY: 12f, durationMs: 260, delayMs: delay);
                delay += 35;
            }

            AnimationHelper.AnimateEntrance(panel, fromY: 18f, durationMs: 300, delayMs: delay);
        }

        private void FadeInPanelForReturn(Panel panel)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(panel);
            visual.Opacity = 0;

            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0f, 0f);
            animation.InsertKeyFrame(1f, 1f, easing);
            animation.Duration = TimeSpan.FromMilliseconds(280);
            animation.Target = "Opacity";

            visual.StartAnimation("Opacity", animation);
        }

        private void ShowOptionsPanel(bool animate)
        {
            ResetOobeViewport();
            WelcomeText.Visibility = Visibility.Visible;
            InstructionText.Visibility = Visibility.Visible;
            HideAllOobePanels();
            OptionsPanel.Visibility = Visibility.Visible;

            if (animate)
            {
                if (_hasShownOptionsHeader)
                {
                    FadeInPanelForReturn(OptionsPanel);
                }
                else
                {
                    AnimateOobePanel(OptionsPanel, includeHeader: true);
                    _hasShownOptionsHeader = true;
                }
            }
        }

        private void ShowCloudChoicePanel(bool animate = true)
        {
            ResetOobeViewport();
            WelcomeText.Visibility = Visibility.Collapsed;
            InstructionText.Visibility = Visibility.Collapsed;
            HideAllOobePanels();
            CloudChoicePanel.Visibility = Visibility.Visible;

            if (animate)
            {
                AnimateOobePanel(CloudChoicePanel);
            }
        }

        private void ShowTokenInputPanel(string title, string subtitle, bool animate = true)
        {
            ResetOobeViewport();
            WelcomeText.Visibility = Visibility.Collapsed;
            InstructionText.Visibility = Visibility.Collapsed;
            HideAllOobePanels();
            TokenInputPanel.Visibility = Visibility.Visible;
            AutoRegisterButton.Visibility = Visibility.Collapsed;

            if (TokenInputTitle != null) TokenInputTitle.Text = title;
            if (TokenInputSubtitle != null) TokenInputSubtitle.Text = subtitle;

            TokenBox.Password = string.Empty;

            if (animate)
            {
                AnimateOobePanel(TokenInputPanel);
            }

            TokenBox.Focus(FocusState.Programmatic);
        }

        private void ShowDeviceAuthPanel(bool animate = true)
        {
            ResetOobeViewport();
            WelcomeText.Visibility = Visibility.Collapsed;
            InstructionText.Visibility = Visibility.Collapsed;
            HideAllOobePanels();

            DeviceAuthPanel.Visibility = Visibility.Visible;
            NamespaceBox.Text = string.Empty;
            AuthPasswordBox.Password = string.Empty;
            AuthErrorText.Visibility = Visibility.Collapsed;
            AuthSubmitButton.IsEnabled = true;
            AuthSubmitButton.Content = "认证并登录";

            if (animate)
            {
                AnimateOobePanel(DeviceAuthPanel);
            }
        }

        private async Task FadeVisualOpacityAsync(UIElement element, float from, float to, double durationMs)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.19f, 1f), new Vector2(0.22f, 1f));

            visual.Opacity = from;

            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1f, to, easing);
            animation.Duration = TimeSpan.FromMilliseconds(durationMs);

            visual.StartAnimation("Opacity", animation);
            await Task.Delay(animation.Duration);
        }

        private void StartDoubleAnimation(
            DependencyObject target,
            string property,
            double from,
            double to,
            double durationMs,
            double beginTimeMs,
            EasingFunctionBase easing)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                BeginTime = TimeSpan.FromMilliseconds(beginTimeMs),
                EnableDependentAnimation = true,
                EasingFunction = easing
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Begin();
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowFormAsync();
        }

        private void RestoreWindowState()
        {
            try
            {
                var settings = AppSettings.Values;

                if (settings.ContainsKey("InitWindow_Width") && settings.ContainsKey("InitWindow_Height"))
                {
                    int width = Math.Max(400, (int)(double)settings["InitWindow_Width"]);
                    int height = Math.Max(300, (int)(double)settings["InitWindow_Height"]);
                    this.AppWindow.Resize(new SizeInt32(width, height));
                }

                if (settings.ContainsKey("InitWindow_X") && settings.ContainsKey("InitWindow_Y"))
                {
                    int x = (int)(double)settings["InitWindow_X"];
                    int y = (int)(double)settings["InitWindow_Y"];
                    this.AppWindow.Move(new PointInt32(x, y));
                }

                if (settings.ContainsKey("InitWindow_State"))
                {
                    string? state = settings["InitWindow_State"] as string;
                    if (state == "Maximized" && this.AppWindow.Presenter is OverlappedPresenter presenter)
                    {
                        presenter.Maximize();
                    }
                }
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                var settings = AppSettings.Values;
                settings["InitWindow_X"] = (double)this.AppWindow.Position.X;
                settings["InitWindow_Y"] = (double)this.AppWindow.Position.Y;
                settings["InitWindow_Width"] = (double)this.AppWindow.Size.Width;
                settings["InitWindow_Height"] = (double)this.AppWindow.Size.Height;

                if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    settings["InitWindow_State"] = presenter.State.ToString();
                }
            }
            catch { }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TokenBox.Password))
            {
                return;
            }

            FinalizeLogin(TokenBox.Password);
        }

        private void TutorialButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://520.re/csh",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        private void FirstUseCard_Tapped(object sender, RoutedEventArgs e)
        {
            ShowCloudChoicePanel();
        }

        private void RegisteredCard_Tapped(object sender, RoutedEventArgs e)
        {
            ShowDeviceAuthPanel();
        }

        private void KvCard_Tapped(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://kv.houlang.cloud",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        private void InputTokenCard_Tapped(object sender, RoutedEventArgs e)
        {
            ShowTokenInputPanel("输入 Token", "使用已有 KV 授权令牌登录");
        }

        private void LocalModeCard_Tapped(object sender, RoutedEventArgs e)
        {
            FinalizeLogin("", "本地存储");
        }

        private void CloudSyncButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTokenInputPanel("云同步", "点击自动注册设备，或输入已有 KV 授权令牌");
            AutoRegisterButton.Visibility = Visibility.Visible;
        }

        private async void AutoRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AutoRegisterButton.IsEnabled = false;
                AutoRegisterButton.Content = "正在注册...";

                string uuid = Guid.NewGuid().ToString();
                string deviceName = $"Classworks Desktop-{Environment.MachineName}";
                string serverUrl = AppSettings.Values["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev";
                serverUrl = serverUrl.TrimEnd('/');

                using var httpClient = new HttpClient();
                
                // 1. Register device
                var devicePayload = new { uuid, deviceName };
                var deviceContent = new StringContent(JsonSerializer.Serialize(devicePayload), Encoding.UTF8, "application/json");
                await httpClient.PostAsync($"{serverUrl}/devices", deviceContent);

                // 2. Get token
                var tokenPayload = new { @namespace = uuid, password = "", appId = "d158067f53627d2b98babe8bffd2fd7d" };
                var tokenContent = new StringContent(JsonSerializer.Serialize(tokenPayload), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"{serverUrl}/apps/auth/token", tokenContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("token", out var tokenElement))
                    {
                        string token = tokenElement.GetString() ?? "";
                        FinalizeLogin(token);
                        return;
                    }
                }

                // If failed to get token, reset button
                AutoRegisterButton.IsEnabled = true;
                AutoRegisterButton.Content = "注册失败，请重试";
            }
            catch
            {
                AutoRegisterButton.IsEnabled = true;
                AutoRegisterButton.Content = "网络错误，请重试";
            }
        }

        private void LocalOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            FinalizeLogin("", "本地存储");
        }

        private void BackToOptions_Click(object sender, RoutedEventArgs e)
        {
            ShowOptionsPanel(animate: true);
        }

        private async void AuthSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NamespaceBox.Text))
            {
                AuthErrorText.Text = "请输入命名空间";
                AuthErrorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                AuthSubmitButton.IsEnabled = false;
                AuthSubmitButton.Content = "正在认证...";
                AuthErrorText.Visibility = Visibility.Collapsed;

                string serverUrl = AppSettings.Values["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev";
                serverUrl = serverUrl.TrimEnd('/');

                using var httpClient = new HttpClient();
                
                string ns = NamespaceBox.Text.Trim();
                string pwd = AuthPasswordBox.Password;

                var tokenPayload = new Dictionary<string, string>
                {
                    { "namespace", ns },
                    { "appId", "d158067f53627d2b98babe8bffd2fd7d" }
                };
                
                if (!string.IsNullOrEmpty(pwd))
                {
                    tokenPayload["password"] = pwd;
                }

                var tokenContent = new StringContent(JsonSerializer.Serialize(tokenPayload), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"{serverUrl}/apps/auth/token", tokenContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("success", out var successElement) && successElement.GetBoolean() == true)
                    {
                        if (doc.RootElement.TryGetProperty("token", out var tokenElement))
                        {
                            string token = tokenElement.GetString() ?? "";
                            if (doc.RootElement.TryGetProperty("device", out var deviceElement) && deviceElement.TryGetProperty("uuid", out var uuidElement))
                            {
                                AppSettings.Values["Settings_DeviceUuid"] = uuidElement.GetString() ?? "";
                            }

                            FinalizeLogin(token);
                            return;
                        }
                    }
                    AuthErrorText.Text = "认证失败，请检查 Namespace 和 Password";
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        AuthErrorText.Text = "密码错误或无权限访问";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        AuthErrorText.Text = "设备不存在，请检查 namespace 是否正确";
                    }
                    else
                    {
                        try
                        {
                            using var errorDoc = JsonDocument.Parse(responseString);
                            if (errorDoc.RootElement.TryGetProperty("error", out var errorObj) && errorObj.TryGetProperty("message", out var msgObj))
                            {
                                AuthErrorText.Text = msgObj.GetString() ?? "认证失败，请稍后重试";
                            }
                            else
                            {
                                AuthErrorText.Text = "认证失败，请稍后重试";
                            }
                        }
                        catch
                        {
                            AuthErrorText.Text = $"认证失败，HTTP 状态码: {(int)response.StatusCode}";
                        }
                    }
                }
                
                AuthErrorText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                AuthErrorText.Text = $"网络错误或认证失败: {ex.Message}";
                AuthErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                AuthSubmitButton.IsEnabled = true;
                AuthSubmitButton.Content = "认证并登录";
            }
        }
    }
}
