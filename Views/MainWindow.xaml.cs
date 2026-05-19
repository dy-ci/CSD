using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;




namespace CSD.Views
{
    public sealed class HomeworkItem
    {
        public string Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public sealed partial class MainWindow : Window
    {
        private const string TokenSettingsKey = "Token";
        private const string ServerUrlKey = "Settings_ServerUrl";
        private const string AutoRefreshEnabledKey = "Settings_AutoRefreshEnabled";
        private const string AutoRefreshIntervalKey = "Settings_AutoRefreshInterval";
        private const string CarouselIntervalKey = "Settings_CarouselInterval";
        private const string CarouselFontSizeKey = "Settings_CarouselFontSize";
        private const string DebugModeKey = "Settings_DebugMode";

        private readonly HttpClient _httpClient = new();
        private int _loadingSequence = 0;
        private DateTime _currentDate = DateTime.Now;
        private string? _rawJson;
        private readonly DispatcherTimer _autoRefreshTimer = new();
        private List<HomeworkItem> _carouselItems = new();
        private DebugWindow? _debugWindow;
        private bool _isUpdatingCalendarSelection;
        private bool _isContentDialogOpen;

        // 当前作业的科目名称集合（用于判断未完成作业）
        private HashSet<string> _currentHomeworkSubjects = new();

        private string BaseUrl
        {
            get
            {
                var url = AppSettings.Values[ServerUrlKey] as string;
                return string.IsNullOrWhiteSpace(url) ? "https://kv-service.wuyuan.dev" : url;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ConfigureIntegratedTitleBar();
            VisualHelper.ApplyWindowBackdrop(this);

            // 设置窗口标题栏图标
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch { }

            // 检查并创建桌面快捷方式
            EnsureDesktopShortcut();

            RestoreWindowState();

            // 关闭时保存窗口状态
            AppWindow.Closing += (sender, args) =>
            {
                SaveWindowState();
            };

            Closed += (sender, args) => SaveWindowState();

            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            RestartAutoRefreshTimer();
            if (Content is FrameworkElement rootContent)
            {
                rootContent.Loaded += RootContent_Loaded;
            }

            UpdateDateDisplay();
            _ = LoadHomeworkAsync(_currentDate);
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
                AnimationHelper.AnimateEntrance(rootContent, fromY: 16f, durationMs: 380);
                AnimationHelper.ApplyStandardInteractions(rootContent);
            }
            _ = CheckForUpdatesAsync();
        }

        private void EnsureDesktopShortcut()
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var shortcutPath = Path.Combine(desktopPath, "CSD.lnk");

                if (File.Exists(shortcutPath))
                    return;

                var exePath = Path.Combine(AppContext.BaseDirectory, "CSD.exe");
                if (!File.Exists(exePath))
                    return;

                // 使用 WSH (Windows Script Host) 创建快捷方式
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = AppContext.BaseDirectory;
                shortcut.Description = "CSD - Classworks Desktop";

                // 设置图标
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    shortcut.IconLocation = $"{iconPath},0";
                }

                shortcut.Save();
            }
            catch { }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateService = new UpdateService();
                var updateInfo = await updateService.CheckForUpdateAsync();

                if (updateInfo?.HasUpdate == true)
                {
                    // 有更新时启动关于按钮闪烁动画
                    StartAboutButtonBlinkAnimation();
                }
            }
            catch { }
        }

        private Storyboard? _aboutButtonBlinkStoryboard;

        private void StartAboutButtonBlinkAnimation()
        {
            if (_aboutButtonBlinkStoryboard != null)
                return;

            _aboutButtonBlinkStoryboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(animation, AboutButton);
            Storyboard.SetTargetProperty(animation, "(UIElement.Opacity)");

            _aboutButtonBlinkStoryboard.Children.Add(animation);
            _aboutButtonBlinkStoryboard.Begin();
        }

        private void StopAboutButtonBlinkAnimation()
        {
            if (_aboutButtonBlinkStoryboard != null)
            {
                _aboutButtonBlinkStoryboard.Stop();
                _aboutButtonBlinkStoryboard = null;
            }
            AboutButton.Opacity = 1.0;
        }

        private async Task<ContentDialogResult?> ShowContentDialogSafelyAsync(ContentDialog dialog)
        {
            if (_isContentDialogOpen)
            {
                return null;
            }

            _isContentDialogOpen = true;
            try
            {
                return await dialog.ShowAsync();
            }
            finally
            {
                _isContentDialogOpen = false;
            }
        }

        /// <summary>
        /// 显示作业编辑器对话框（多输入框模式）
        /// </summary>
        private async Task<string?> ShowHomeworkEditorDialogAsync(string title, string initialText, XamlRoot xamlRoot)
        {
            // 解析初始文本为多行
            var lines = ParseLines(initialText);
            
            // 检查是否为本地模式
            string dataProvider = AppSettings.Values["Settings_DataProvider"] as string ?? "";
            bool isLocalMode = dataProvider == "本地存储";
            if (isLocalMode)
            {
                title += " (本地模式)";
            }

            // 输入框容器
            var linesPanel = new StackPanel { Spacing = 8 };
            var inputBoxes = new List<TextBox>();
            
            // 预览区域（提前创建以便引用）
            var previewBorder = new Border
            {
                Padding = new Thickness(12),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                MinHeight = 60
            };
            
            // 更新预览
            void RefreshPreview()
            {
                var content = GetContent();
                try
                {
                    previewBorder.Child = MarkdownTextRenderer.CreateRichTextBlock(
                        content,
                        15,
                        (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]);
                }
                catch
                {
                    previewBorder.Child = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap };
                }
            }
            
            // 添加一行输入框（在指定位置插入）
            void AddLine(string text = "", int insertIndex = -1)
            {
                var box = new TextBox
                {
                    Text = text,
                    PlaceholderText = "输入内容...",
                    FontSize = 14,
                    Padding = new Thickness(8, 6, 8, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                
                // 文本变化时更新预览
                box.TextChanged += (_, _) => RefreshPreview();
                
                // 粘贴多行文本：自动拆分为多行
                box.Paste += (s, e) =>
                {
                    var textBox = (TextBox)s!;
                    var clipboardContent = textBox.SelectedText;
                    
                    // 尝试从剪贴板获取内容
                    try
                    {
                        var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                        if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                        {
                            clipboardContent = dataPackageView.GetTextAsync().AsTask().GetAwaiter().GetResult() ?? string.Empty;
                        }
                    }
                    catch { }
                    
                    if (string.IsNullOrEmpty(clipboardContent))
                        return;
                    
                    // 规范化换行符
                    clipboardContent = clipboardContent.Replace("\r\n", "\n").Replace("\r", "\n");
                    
                    // 如果粘贴内容包含换行，拆分为多行
                    if (clipboardContent.Contains('\n'))
                    {
                        e.Handled = true;
                        var index = inputBoxes.IndexOf(box);
                        var lines = clipboardContent.Split('\n');
                        
                        // 第一行内容放到当前输入框（替换选中文本）
                        var beforeSelection = box.Text.Substring(0, box.SelectionStart);
                        var afterSelection = box.Text.Substring(box.SelectionStart + box.SelectionLength);
                        box.Text = beforeSelection + lines[0] + afterSelection;
                        box.SelectionStart = beforeSelection.Length + lines[0].Length;
                        RefreshPreview();
                        
                        // 后续行插入为新的输入框
                        for (int li = 1; li < lines.Length; li++)
                        {
                            var insertPos = index + li;
                            // 最后一行拼接 afterSelection
                            var lineText = lines[li];
                            if (li == lines.Length - 1 && !string.IsNullOrEmpty(afterSelection))
                                lineText += afterSelection;
                            AddLine(lineText, insertPos);
                        }
                        
                        // 聚焦到最后一行
                        inputBoxes[index + lines.Length - 1].Focus(FocusState.Programmatic);
                    }
                };
                
                // 回车：在当前行下方插入新行并聚焦
                box.KeyDown += (s, e) =>
                {
                    if (e.Key == Windows.System.VirtualKey.Enter)
                    {
                        e.Handled = true;
                        var index = inputBoxes.IndexOf(box);
                        AddLine("", index + 1); // 在当前行下方插入
                        inputBoxes[index + 1].Focus(FocusState.Programmatic);
                    }
                    // 退格：空行时删除当前行，聚焦到上一行末尾
                    else if (e.Key == Windows.System.VirtualKey.Back)
                    {
                        var index = inputBoxes.IndexOf(box);
                        // 只有当前行为空且不是第一行时才删除
                        if (string.IsNullOrEmpty(box.Text) && index > 0)
                        {
                            e.Handled = true;
                            var prevBox = inputBoxes[index - 1];
                            
                            // 移除当前行
                            linesPanel.Children.RemoveAt(index);
                            inputBoxes.RemoveAt(index);
                            
                            // 聚焦到上一行末尾
                            prevBox.Focus(FocusState.Programmatic);
                            prevBox.SelectionStart = prevBox.Text.Length;
                            RefreshPreview();
                        }
                    }
                    // 上方向键：光标在行首时跳转到上一行末尾
                    else if (e.Key == Windows.System.VirtualKey.Up)
                    {
                        var index = inputBoxes.IndexOf(box);
                        if (index > 0 && box.SelectionStart == 0)
                        {
                            e.Handled = true;
                            var prevBox = inputBoxes[index - 1];
                            prevBox.Focus(FocusState.Programmatic);
                            prevBox.SelectionStart = prevBox.Text.Length;
                        }
                    }
                    // 下方向键：光标在行末时跳转到下一行开头
                    else if (e.Key == Windows.System.VirtualKey.Down)
                    {
                        var index = inputBoxes.IndexOf(box);
                        if (index < inputBoxes.Count - 1 && box.SelectionStart == box.Text.Length)
                        {
                            e.Handled = true;
                            var nextBox = inputBoxes[index + 1];
                            nextBox.Focus(FocusState.Programmatic);
                            nextBox.SelectionStart = 0;
                        }
                    }
                };
                
                // 插入到指定位置或末尾
                if (insertIndex >= 0 && insertIndex < inputBoxes.Count)
                {
                    inputBoxes.Insert(insertIndex, box);
                    linesPanel.Children.Insert(insertIndex, box);
                }
                else
                {
                    inputBoxes.Add(box);
                    linesPanel.Children.Add(box);
                }
            }
            
            // 获取所有行内容
            string GetContent()
            {
                var nonEmptyLines = inputBoxes
                    .Select(b => b.Text.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
                return string.Join("\n", nonEmptyLines);
            }
            
            // 初始化输入框
            if (lines.Count == 0 || (lines.Count == 1 && string.IsNullOrEmpty(lines[0])))
            {
                AddLine("");
            }
            else
            {
                foreach (var line in lines)
                {
                    AddLine(line);
                }
            }
            
            // 初始预览
            RefreshPreview();
            
            // 添加按钮
            var addLineBtn = new Button
            {
                Content = "+ 添加新行",
                FontSize = 13,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 4, 0, 0)
            };
            addLineBtn.Click += (s, e) =>
            {
                AddLine(""); // 添加到末尾
                inputBoxes[^1].Focus(FocusState.Programmatic);
            };
            
            // ========== 快捷面板 ==========
            
            // 获取当前聚焦的输入框
            TextBox? GetFocusedBox()
            {
                foreach (var b in inputBoxes)
                {
                    if (b.FocusState != FocusState.Unfocused)
                        return b;
                }
                return inputBoxes.Count > 0 ? inputBoxes[^1] : null;
            }
            
            // 在光标位置插入文本
            void InsertAtCursor(string before, string after = "")
            {
                var box = GetFocusedBox();
                if (box == null) return;
                
                var start = box.SelectionStart;
                var selectedText = box.SelectedText;
                var replacement = before + (selectedText.Length > 0 ? selectedText : "文字") + after;
                
                box.Text = box.Text.Substring(0, start) + replacement + box.Text.Substring(start + box.SelectionLength);
                
                // 选中插入的占位文字
                box.SelectionStart = start + before.Length;
                box.SelectionLength = selectedText.Length > 0 ? selectedText.Length : 2;
                box.Focus(FocusState.Programmatic);
                RefreshPreview();
            }
            
            // 在当前行前插入新行
            void InsertNewLine(string text)
            {
                var box = GetFocusedBox();
                if (box == null) return;
                
                var index = inputBoxes.IndexOf(box);
                AddLine(text, index);
                inputBoxes[index].Focus(FocusState.Programmatic);
                inputBoxes[index].SelectionStart = inputBoxes[index].Text.Length;
                RefreshPreview();
            }
            
            // 构建快捷面板内容
            StackPanel BuildShortcutPanel()
            {
                var panel = new StackPanel { Spacing = 6 };
                
                // MD 区
                panel.Children.Add(new TextBlock
                {
                    Text = "Markdown",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 4, 0, 2)
                });
                
                var mdGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                    },
                    RowSpacing = 4,
                    ColumnSpacing = 4
                };
                
                void AddToolToGrid(Grid grid, int row, int col, string label, string tooltip, string before, string after = "", bool newLine = false)
                {
                    var btn = new Button
                    {
                        Content = label,
                        FontSize = 12,
                        Padding = new Thickness(6, 4, 6, 4),
                        MinWidth = 0,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    ToolTipService.SetToolTip(btn, tooltip);
                    var cb = before; var ca = after; var cn = newLine;
                    btn.Click += (s, e) =>
                    {
                        if (cn) InsertNewLine(cb);
                        else InsertAtCursor(cb, ca);
                    };
                    Grid.SetRow(btn, row);
                    Grid.SetColumn(btn, col);
                    grid.Children.Add(btn);
                }
                
                // MD 按钮网格
                AddToolToGrid(mdGrid, 0, 0, "B", "粗体 **粗体**", "**", "**");
                AddToolToGrid(mdGrid, 0, 1, "I", "斜体 *斜体*", "*", "*");
                AddToolToGrid(mdGrid, 0, 2, "S", "删除线 ~~删除线~~", "~~", "~~");
                AddToolToGrid(mdGrid, 0, 3, "`", "行内代码 `代码`", "`", "`");
                AddToolToGrid(mdGrid, 1, 0, "H1", "一级标题", "# ", newLine: true);
                AddToolToGrid(mdGrid, 1, 1, "H2", "二级标题", "## ", newLine: true);
                AddToolToGrid(mdGrid, 1, 2, "H3", "三级标题", "### ", newLine: true);
                AddToolToGrid(mdGrid, 1, 3, ">", "引用", "> ", newLine: true);
                AddToolToGrid(mdGrid, 2, 0, "•", "无序列表", "- ", newLine: true);
                AddToolToGrid(mdGrid, 2, 1, "1.", "有序列表", "1. ", newLine: true);
                AddToolToGrid(mdGrid, 2, 2, "[]", "链接 [文本](url)", "[", "](url)");
                AddToolToGrid(mdGrid, 2, 3, "```", "代码块", "```\n代码\n```", newLine: true);
                
                panel.Children.Add(mdGrid);
                
                // MFM 区
                panel.Children.Add(new TextBlock
                {
                    Text = "MFM",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 2)
                });
                
                var mfmGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                    },
                    RowSpacing = 4,
                    ColumnSpacing = 4
                };
                
                // MFM 按钮网格
                AddToolToGrid(mfmGrid, 0, 0, "x2", "放大", "$[x2 ", "]");
                AddToolToGrid(mfmGrid, 0, 1, "x3", "放大", "$[x3 ", "]");
                AddToolToGrid(mfmGrid, 0, 2, "x4", "放大", "$[x4 ", "]");
                AddToolToGrid(mfmGrid, 0, 3, "tada", "动画", "$[tada ", "]");
                AddToolToGrid(mfmGrid, 0, 4, "jelly", "果冻", "$[jelly ", "]");
                AddToolToGrid(mfmGrid, 1, 0, "spin", "旋转", "$[spin ", "]");
                AddToolToGrid(mfmGrid, 1, 1, "shake", "摇晃", "$[shake ", "]");
                AddToolToGrid(mfmGrid, 1, 2, "blur", "模糊", "$[blur ", "]");
                AddToolToGrid(mfmGrid, 1, 3, "rainbow", "彩虹", "$[rainbow ", "]");
                AddToolToGrid(mfmGrid, 1, 4, "sparkle", "闪光", "$[sparkle ", "]");
                AddToolToGrid(mfmGrid, 2, 0, "fg", "文字颜色", "$[fg.color=f00 ", "]");
                AddToolToGrid(mfmGrid, 2, 1, "bg", "背景色", "$[bg.color=ff0 ", "]");
                AddToolToGrid(mfmGrid, 2, 2, "font", "字体", "$[font.serif ", "]");
                AddToolToGrid(mfmGrid, 2, 3, "ruby", "注音", "$[ruby ", " 注音]");
                AddToolToGrid(mfmGrid, 2, 4, "@", "提及", "@");
                AddToolToGrid(mfmGrid, 3, 0, "#", "标签", "#");
                AddToolToGrid(mfmGrid, 3, 1, "<s>", "缩小", "<small>", "</small>");
                AddToolToGrid(mfmGrid, 3, 2, "<c>", "居中", "<center>", "</center>");
                AddToolToGrid(mfmGrid, 3, 3, "jump", "跳动", "$[jump ", "]");
                AddToolToGrid(mfmGrid, 3, 4, "flip", "翻转", "$[flip ", "]");
                
                panel.Children.Add(mfmGrid);
                
                return panel;
            }
            
            // ========== 组装界面 ==========
            
            // 左侧快捷面板
            var shortcutPanel = BuildShortcutPanel();
            var shortcutScrollViewer = new ScrollViewer
            {
                Content = shortcutPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(4, 0, 8, 0),
                Width = 280
            };
            
            // 右侧编辑区内容
            var editorContent = new StackPanel
            {
                Spacing = 10,
                Padding = new Thickness(4)
            };
            
            editorContent.Children.Add(new TextBlock
            {
                Text = "预览：",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            editorContent.Children.Add(previewBorder);
            
            editorContent.Children.Add(new TextBlock
            {
                Text = "每行一条内容，回车添加新行：",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            });
            
            editorContent.Children.Add(linesPanel);
            editorContent.Children.Add(addLineBtn);

            // 右侧编辑区整体可滚动
            var editorScrollViewer = new ScrollViewer
            {
                Content = editorContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            // 左右布局
            var mainGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };
            Grid.SetColumn(shortcutScrollViewer, 0);
            Grid.SetColumn(editorScrollViewer, 1);
            mainGrid.Children.Add(shortcutScrollViewer);
            mainGrid.Children.Add(editorScrollViewer);
            
            var dialog = new ContentDialog
            {
                Title = title,
                Content = mainGrid,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                XamlRoot = xamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };
            
            var result = await ShowContentDialogSafelyAsync(dialog);
            
            if (result != ContentDialogResult.Primary)
                return null;
            
            return GetContent();
        }
        
        /// <summary>
        /// 解析文本为多行
        /// </summary>
        private static List<string> ParseLines(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();
            
            // 统一换行符
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Split('\n').ToList();
        }

        private void RestoreWindowState()
        {
            try
            {
                var settings = AppSettings.Values;

                if (settings.ContainsKey("MainWindow_Width") && settings.ContainsKey("MainWindow_Height"))
                {
                    int width = Math.Max(400, (int)(double)settings["MainWindow_Width"]);
                    int height = Math.Max(300, (int)(double)settings["MainWindow_Height"]);
                    this.AppWindow.Resize(new SizeInt32(width, height));
                }

                if (settings.ContainsKey("MainWindow_X") && settings.ContainsKey("MainWindow_Y"))
                {
                    int x = (int)(double)settings["MainWindow_X"];
                    int y = (int)(double)settings["MainWindow_Y"];
                    this.AppWindow.Move(new PointInt32(x, y));
                }

                if (settings.ContainsKey("MainWindow_State"))
                {
                    string? state = settings["MainWindow_State"] as string;
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
                settings["MainWindow_X"] = (double)this.AppWindow.Position.X;
                settings["MainWindow_Y"] = (double)this.AppWindow.Position.Y;
                settings["MainWindow_Width"] = (double)this.AppWindow.Size.Width;
                settings["MainWindow_Height"] = (double)this.AppWindow.Size.Height;

                if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    settings["MainWindow_State"] = presenter.State.ToString();
                }
            }
            catch { }
        }

        private void RestartAutoRefreshTimer()
        {
            _autoRefreshTimer.Stop();

            var settings = AppSettings.Values;
            bool enabled = settings.ContainsKey(AutoRefreshEnabledKey)
                ? (bool)(settings[AutoRefreshEnabledKey] ?? false)
                : false;

            if (enabled)
            {
                double intervalSeconds = (double)(settings[AutoRefreshIntervalKey] ?? 60.0);
                _autoRefreshTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
                _autoRefreshTimer.Start();
            }
        }

        private void AutoRefreshTimer_Tick(object? sender, object e)
        {
            _ = RefreshAllGlobalComponentsAsync();
        }

        /// <summary>
        /// 自动刷新与手动刷新共用：重新拉取当前日作业并更新主界面相关区域（含未完成列表等）。
        /// </summary>
        private async Task RefreshAllGlobalComponentsAsync()
        {
            // 如果已有缓存数据，先用缓存重新渲染（确保字体大小等设置立即生效）
            if (!string.IsNullOrWhiteSpace(_rawJson))
            {
                await ShowHomeworkAsync(_rawJson);
            }
            // 再从服务器拉取最新数据
            await LoadHomeworkAsync(_currentDate);
        }

        private async void RefreshHomeworkButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadHomeworkAsync(_currentDate);
        }

        private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(() =>
            {
                RestartAutoRefreshTimer();
                _ = RefreshAllGlobalComponentsAsync();
                VisualHelper.ApplyWindowBackdrop(this);

                // 如果关闭了调试模式，关闭调试窗口
                if (!DebugWindow.IsDebugModeEnabled() && _debugWindow != null)
                {
                    _debugWindow.Close();
                    _debugWindow = null;
                }
            });
            settingsWindow.Activate();
        }

        private void TutorialButton_Click(object sender, RoutedEventArgs e)
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://520.re/csh"));
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止闪烁动画
            StopAboutButtonBlinkAnimation();

            var aboutWindow = new AboutWindow();
            aboutWindow.Activate();
        }

        private void AttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            var attendanceWindow = new AttendanceWindow(_currentDate);
            attendanceWindow.Activate();
        }

        private async void PrevDateButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddDays(-1);
            await LoadHomeworkAsync(_currentDate);
        }

        private async void NextDateButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddDays(1);
            await LoadHomeworkAsync(_currentDate);
        }

        private void DateFlyout_Opening(object sender, object e)
        {
            UpdateDateDisplay();
        }

        private async void DateCalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (_isUpdatingCalendarSelection || sender.SelectedDates.Count == 0)
            {
                return;
            }

            var selectedDate = sender.SelectedDates[0].Date;
            if (DateFlyout.IsOpen)
            {
                DateFlyout.Hide();
            }

            _currentDate = selectedDate.Date;
            await LoadHomeworkAsync(_currentDate);
        }

        private void UpdateDateDisplay()
        {
            CurrentDateTitleText.Text = _currentDate.Date == DateTime.Today.Date
                ? "今日作业"
                : _currentDate.ToString("yyyy年M月d日");

            TodayKeyText.Text = _currentDate.Date == DateTime.Today.Date
                ? $"{_currentDate:yyyy-MM-dd} · {_currentDate:dddd} · 点击选择日期"
                : $"{_currentDate:yyyy-MM-dd} · {_currentDate:dddd} · 点击切换日期";

            _isUpdatingCalendarSelection = true;
            DateCalendarView.SelectedDates.Clear();
            DateCalendarView.SelectedDates.Add(_currentDate);
            DateCalendarView.SetDisplayDate(_currentDate);
            _isUpdatingCalendarSelection = false;
        }

        private void UpdateStatus(string message)
        {
            string dataProvider = AppSettings.Values["Settings_DataProvider"] as string ?? "";
            if (dataProvider == "本地存储")
            {
                StatusText.Text = message + " (本地模式)";
            }
            else
            {
                StatusText.Text = message;
            }
        }

        private async Task LoadHomeworkAsync(DateTime date)
        {
            // 增加版本号并记录当前版本
            int currentSequence = ++_loadingSequence;

            _currentDate = date.Date;
            UpdateDateDisplay();

            var dateKey = $"classworks-data-{date:yyyyMMdd}";

            bool isToday = date.Date == DateTime.Now.Date;
            UpdateStatus(isToday
                ? "正在加载今日作业..."
                : $"正在加载 {date:yyyy-MM-dd} 的作业...");
            HomeworkContainer.Children.Clear();

            var responseBody = await SendKvRequestAsync(HttpMethod.Get, $"/kv/{Uri.EscapeDataString(dateKey)}");
            
            // 如果已有更新的请求，放弃本次结果
            if (currentSequence != _loadingSequence)
                return;

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _currentHomeworkSubjects.Clear();
                await LoadUndoneHomeworkAsync(currentSequence);
                return;
            }

            _rawJson = responseBody;
            await ShowHomeworkAsync(responseBody);

            if (currentSequence != _loadingSequence)
                return;

            await LoadUndoneHomeworkAsync(currentSequence);
        }

        private async Task<string?> SendKvRequestAsync(HttpMethod method, string path, string? jsonBody = null, CancellationToken cancellationToken = default)
        {
            var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
            if (dataProvider == "本地存储")
            {
                var localResponse = await LocalKvStorageEngine.HandleRequestAsync(method, path, jsonBody);
                if (localResponse.IsSuccessStatusCode)
                {
                    return await localResponse.Content.ReadAsStringAsync();
                }
                return null;
            }

            var token = AppSettings.Values[TokenSettingsKey] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                UpdateStatus("本地没有 Token，请先完成初始化。");
                return null;
            }

            try
            {
                using var request = new HttpRequestMessage(method, BaseUrl + path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                if (jsonBody is not null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync();

                // 调试日志
                LogToDebugWindow(method.Method, path, (int)response.StatusCode, responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        if (path.StartsWith("/kv/classworks-data-", StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateStatus("当天没有布置作业，请点击按钮布置");
                        }
                        // 对于其他的 404（例如科目配置或名单配置尚未创建），静默返回，不覆盖主界面的状态提示
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        UpdateStatus("token配置错误，请去设置销毁重设");
                    }
                    else
                    {
                        UpdateStatus($"请求失败 ({(int)response.StatusCode})");
                    }
                    return null;
                }

                return responseBody;
            }
            catch (Exception ex)
            {
                UpdateStatus("网络请求失败。");

                // 调试日志（记录错误）
                LogToDebugWindow(method.Method, path, 0, "", ex.Message);

                return null;
            }
        }

        private void LogToDebugWindow(string method, string path, int statusCode, string responseBody, string? errorMessage = null)
        {
            if (!DebugWindow.IsDebugModeEnabled())
                return;

            // 确保调试窗口已创建
            if (_debugWindow == null)
            {
                _debugWindow = new DebugWindow();
                _debugWindow.Closed += (_, _) => _debugWindow = null;
                Logger.SetDebugWindow(_debugWindow);
            }

            _debugWindow.Activate();
            _debugWindow.AppendLog(method, path, statusCode, responseBody, errorMessage);
        }

        private async Task ShowHomeworkAsync(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("homework", out var homework) || homework.ValueKind != JsonValueKind.Object)
                {
                    UpdateStatus("当天没有布置作业，请点击按钮布置");
                    HomeworkContainer.Children.Clear();
                    return;
                }

                var items = new List<HomeworkItem>();
                _currentHomeworkSubjects.Clear();
                foreach (var subject in homework.EnumerateObject())
                {
                    var content = GetNormalizedHomeworkContent(subject.Value);

                    // 内容为空时不显示为卡片
                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    items.Add(new HomeworkItem
                    {
                        Subject = subject.Name,
                        Content = content
                    });
                    _currentHomeworkSubjects.Add(subject.Name);
                }

                HomeworkContainer.Children.Clear();
                UpdateStatus(items.Count == 0 ? "当天没有布置作业，请点击按钮布置" : $"共 {items.Count} 项作业");

                // 更新轮播数据，退出轮播模式
                _carouselItems = items;

                if (items.Count == 0) return;

                // 从设置读取卡片大小参数
                var settings = AppSettings.Values;
                double minCardWidth = (double)(settings["Settings_MinCardWidth"] ?? 220.0);
                double gap = (double)(settings["Settings_CardGap"] ?? 12.0);
                double subjectFontSize = (double)(settings["Settings_SubjectFontSize"] ?? 18.0);
                double contentFontSize = (double)(settings["Settings_ContentFontSize"] ?? 15.0);

                // 智能自适应布局：卡片宽度按内容长度比例分配
                double availableWidth = HomeworkContainer.ActualWidth;
                if (availableWidth <= 0) availableWidth = 800;

                var rows = BuildCardRows(items, availableWidth, minCardWidth, gap, contentFontSize);

                foreach (var rowItems in rows)
                {
                    // 计算当前行的总权重（基于内容长度）
                    var row = new Grid
                    {
                        ColumnSpacing = gap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    // 计算每个卡片的权重（内容长度）
                    var weights = new List<double>();
                    double totalWeight = 0;
                    foreach (var item in rowItems)
                    {
                        double weight = EstimateCardWidth(item, minCardWidth, availableWidth, contentFontSize);
                        weights.Add(weight);
                        totalWeight += weight;
                    }

                    // 按比例分配列宽
                    for (int column = 0; column < rowItems.Count; column++)
                    {
                        double proportion = weights[column] / totalWeight;
                        row.ColumnDefinitions.Add(new ColumnDefinition
                        {
                            Width = new GridLength(proportion, GridUnitType.Star)
                        });
                    }

                    for (int column = 0; column < rowItems.Count; column++)
                    {
                        var card = CreateCard(rowItems[column], subjectFontSize, contentFontSize);
                        Grid.SetColumn(card, column);
                        row.Children.Add(card);
                    }

                    HomeworkContainer.Children.Add(row);
                }
            }
            catch (JsonException)
            {
                StatusText.Text = "作业数据格式错误。";
                HomeworkContainer.Children.Clear();
            }
        }

        private static string GetNormalizedHomeworkContent(JsonElement element)
        {
            string? content = null;

            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("content", out var contentElement))
            {
                content = contentElement.ValueKind == JsonValueKind.String
                    ? contentElement.GetString()
                    : contentElement.ToString();
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                content = element.GetString();
            }
            else
            {
                content = element.ToString();
            }

            return MarkdownTextRenderer.NormalizeStorageText(content);
        }

        private static List<List<HomeworkItem>> BuildCardRows(
            List<HomeworkItem> items,
            double availableWidth,
            double minCardWidth,
            double gap,
            double contentFontSize)
        {
            var rows = new List<List<HomeworkItem>>();
            var currentRow = new List<HomeworkItem>();
            double currentRowWidth = 0;

            foreach (var item in items)
            {
                double estimatedWidth = EstimateCardWidth(item, minCardWidth, availableWidth, contentFontSize);
                double nextRowWidth = currentRow.Count == 0
                    ? estimatedWidth
                    : currentRowWidth + gap + estimatedWidth;

                bool exceedsWidth = nextRowWidth > availableWidth;
                bool exceedsMaxColumns = currentRow.Count >= 4;

                if (currentRow.Count > 0 && (exceedsWidth || exceedsMaxColumns))
                {
                    rows.Add(currentRow);
                    currentRow = new List<HomeworkItem>();
                    currentRowWidth = 0;
                }

                currentRow.Add(item);
                currentRowWidth = currentRow.Count == 1
                    ? estimatedWidth
                    : currentRowWidth + gap + estimatedWidth;
            }

            if (currentRow.Count > 0)
            {
                rows.Add(currentRow);
            }

            return rows;
        }

        private static double EstimateCardWidth(HomeworkItem item, double minCardWidth, double availableWidth, double contentFontSize)
        {
            int longestLineLength = MarkdownTextRenderer.GetPlainText(item.Content)
                .Split('\n')
                .Select(line => line.Trim().Length)
                .DefaultIfEmpty(0)
                .Max();

            int titleLength = item.Subject.Trim().Length;
            int referenceLength = Math.Max(longestLineLength, titleLength);

            double extraWidth = Math.Max(0, referenceLength - 16) * Math.Max(5, contentFontSize * 0.45);
            double desiredWidth = minCardWidth + extraWidth;
            double maxWidth = Math.Max(minCardWidth, availableWidth);

            return Math.Clamp(desiredWidth, minCardWidth, maxWidth);
        }

        private Button CreateCard(HomeworkItem item, double subjectFontSize, double contentFontSize)
        {
            var border = new Border
            {
                Padding = new Thickness(16),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(16),
                Translation = new Vector3(0, 0, 16),
                VerticalAlignment = VerticalAlignment.Stretch
            };

            border.Shadow = new ThemeShadow();

            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock
            {
                FontSize = subjectFontSize,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Text = item.Subject,
                TextWrapping = TextWrapping.Wrap
            });

            // 添加自动编号的内容
            var contentText = AddAutoNumbering(item.Content);
            stack.Children.Add(MarkdownTextRenderer.CreateRichTextBlock(
                contentText,
                contentFontSize,
                (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]));

            border.Child = stack;

            var button = new Button
            {
                Content = border,
                Tag = item,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };
            button.Click += CardButton_Click;

            return button;
        }

        /// <summary>
        /// 为多行内容添加自动编号（1. 2. 3. ...）
        /// 仅在内容完全没有编号格式时才添加，避免与已有编号冲突
        /// </summary>
        private static string AddAutoNumbering(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;

            // 统一移除所有空行，保持行为一致，避免出现一堆空格（空行）
            var validLines = content.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (validLines.Count == 0)
                return string.Empty;

            // 先检查是否至少有一行有编号（如果有则完全不处理编号，仅返回去除了空行的内容）
            bool hasAnyNumbering = validLines.Any(line => 
                System.Text.RegularExpressions.Regex.IsMatch(line.TrimStart(), @"^\d+\.(\s|$)"));

            if (hasAnyNumbering)
                return string.Join("\n", validLines);

            if (validLines.Count <= 1)
                return string.Join("\n", validLines);

            var numberedLines = validLines.Select((line, index) => $"{index + 1}. {line.Trim()}");
            return string.Join("\n", numberedLines);
        }

        private async void CardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not HomeworkItem item)
                return;

            var editedText = await ShowHomeworkEditorDialogAsync(
                $"修改 {item.Subject} 作业",
                item.Content,
                button.XamlRoot);

            if (editedText is not null)
            {
                await SaveHomeworkAsync(item.Subject, editedText);
            }
        }

        private async Task SaveHomeworkAsync(string subject, string newContent)
        {
            if (string.IsNullOrWhiteSpace(_rawJson))
                return;

            try
            {
                using var document = JsonDocument.Parse(_rawJson);
                var root = new Dictionary<string, JsonElement>();
                foreach (var prop in document.RootElement.EnumerateObject())
                    root[prop.Name] = prop.Value;

                // 构建新的 homework 对象
                var homeworkDict = new Dictionary<string, object>();
                if (document.RootElement.TryGetProperty("homework", out var homeworkElement) && homeworkElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var subj in homeworkElement.EnumerateObject())
                    {
                        if (subj.Name == subject)
                        {
                            homeworkDict[subj.Name] = new Dictionary<string, object> { ["content"] = MarkdownTextRenderer.NormalizeStorageText(newContent) };
                        }
                        else
                        {
                            if (subj.Value.ValueKind == JsonValueKind.Object)
                            {
                                var inner = new Dictionary<string, object>();
                                foreach (var p in subj.Value.EnumerateObject())
                                {
                                    inner[p.Name] = p.Value.ValueKind == JsonValueKind.String
                                        ? p.Value.GetString()!
                                        : p.Value.GetRawText();
                                }
                                homeworkDict[subj.Name] = inner;
                            }
                            else
                            {
                                homeworkDict[subj.Name] = subj.Value.GetRawText();
                            }
                        }
                    }
                }

                // 构建 attendance 对象
                var attendanceDict = new Dictionary<string, object>();
                if (document.RootElement.TryGetProperty("attendance", out var attendanceElement) && attendanceElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var att in attendanceElement.EnumerateObject())
                    {
                        if (att.Value.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<string>();
                            foreach (var item in att.Value.EnumerateArray())
                                list.Add(item.GetString() ?? "");
                            attendanceDict[att.Name] = list;
                        }
                        else
                        {
                            attendanceDict[att.Name] = att.Value.GetRawText();
                        }
                    }
                }

                var payload = new Dictionary<string, object>
                {
                    ["homework"] = homeworkDict,
                    ["attendance"] = attendanceDict
                };

                var json = JsonSerializer.Serialize(payload, AppJsonSerializerContext.Default.DictionaryStringObject);
                var dateKey = $"classworks-data-{_currentDate:yyyyMMdd}";
                var response = await SendKvRequestAsync(HttpMethod.Post, $"/kv/{Uri.EscapeDataString(dateKey)}", json);

                if (response != null)
                {
                    _rawJson = json;
                    await LoadHomeworkAsync(_currentDate);
                }
            }
            catch (Exception)
            {
                UpdateStatus("保存作业失败。");
            }
        }

        // ========== 轮播功能 ==========

        private void ToggleCarouselButton_Click(object sender, RoutedEventArgs e)

                {

                    if (_carouselItems.Count == 0)

                    {

                        UpdateStatus("当天没有布置作业，请点击按钮布置");

                        return;

                    }

        

                    var carouselWindow = new CarouselWindow(_carouselItems);

                    carouselWindow.OnExitCarousel = () =>

                    {

                        // 退出轮播时重新打开主窗口

                        var mainWindow = new MainWindow();

                        mainWindow.Activate();

                    };

        

                    // 保存窗口状态后再关闭

                    SaveWindowState();

                    carouselWindow.Activate();

                    this.Close();

                }

        

        

        

        

        // ========== 未完成作业 ==========

        private async Task LoadUndoneHomeworkAsync(int sequence)
        {
            if (sequence != _loadingSequence)
                return;

            UndoneHomeworkPanel.Children.Clear();

            // 获取全部作业列表
            var listResponse = await SendKvRequestAsync(HttpMethod.Get, $"/kv/{ClassworksKvKeys.SubjectConfig}");
            if (string.IsNullOrWhiteSpace(listResponse))
            {
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(listResponse);
                var allHomework = new List<(int Order, string Name)>();

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("order", out var orderElement) &&
                        element.TryGetProperty("name", out var nameElement))
                    {
                        int order = orderElement.GetInt32();
                        string name = nameElement.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            allHomework.Add((order, name));
                        }
                    }
                }

                // 过滤出未完成的作业（不在当前作业科目中的）
                var undoneHomework = allHomework
                    .Where(h => !_currentHomeworkSubjects.Contains(h.Name))
                    .ToList();

                if (sequence != _loadingSequence)
                    return;

                // 始终显示科目按钮区域
                UndoneHomeworkPanel.Visibility = Visibility.Visible;

                if (undoneHomework.Count == 0)
                {
                    // 添加一个提示按钮
                    var placeholderButton = new Button
                    {
                        Content = "所有科目均已布置作业",
                        Tag = "no_undone",
                        MinWidth = 100
                    };
                    placeholderButton.Click += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("所有科目已完成，暂无补做作业");
                    };
                    AnimationHelper.AttachHoverAnimation(placeholderButton, 1.02f, 0.985f, -2f);
                    AnimationHelper.AnimateEntrance(placeholderButton, fromY: 10f, durationMs: 240);
                    UndoneHomeworkPanel.Children.Add(placeholderButton);
                    return;
                }

                for (int index = 0; index < undoneHomework.Count; index++)
                {
                    if (sequence != _loadingSequence)
                        return;

                    var (order, name) = undoneHomework[index];
                    var button = new Button
                    {
                        Content = $"#{order} {name}",
                        Tag = name,
                        MinWidth = 100
                    };
                    button.Click += UndoneHomeworkButton_Click;
                    AnimationHelper.AttachHoverAnimation(button, 1.02f, 0.985f, -2f);
                    AnimationHelper.AnimateEntrance(button, fromY: 10f, durationMs: 240, delayMs: Math.Min(index, 8) * 30);
                    UndoneHomeworkPanel.Children.Add(button);
                }
            }
            catch (JsonException)
            {
                // 静默失败
            }
        }

        private async void UndoneHomeworkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string homeworkName)
                return;

            var newContent = await ShowHomeworkEditorDialogAsync(
                $"添加 {homeworkName} 作业",
                string.Empty,
                button.XamlRoot);

            if (newContent is null)
                return;

            // 构建新的 homework 对象
            try
            {
                var homeworkDict = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(_rawJson))
                {
                    using var document = JsonDocument.Parse(_rawJson);
                    if (document.RootElement.TryGetProperty("homework", out var homeworkElement) && homeworkElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var subj in homeworkElement.EnumerateObject())
                        {
                            if (subj.Value.ValueKind == JsonValueKind.Object)
                            {
                                var inner = new Dictionary<string, object>();
                                foreach (var p in subj.Value.EnumerateObject())
                                {
                                    inner[p.Name] = p.Value.ValueKind == JsonValueKind.String
                                        ? p.Value.GetString()!
                                        : p.Value.GetRawText();
                                }
                                homeworkDict[subj.Name] = inner;
                            }
                            else
                            {
                                homeworkDict[subj.Name] = subj.Value.GetRawText();
                            }
                        }
                    }
                }

                // 添加新作业
                homeworkDict[homeworkName] = new Dictionary<string, object> { ["content"] = MarkdownTextRenderer.NormalizeStorageText(newContent) };

                // 构建 attendance 对象
                var attendanceDict = new Dictionary<string, object>();
                if (!string.IsNullOrWhiteSpace(_rawJson))
                {
                    using var document = JsonDocument.Parse(_rawJson);
                    if (document.RootElement.TryGetProperty("attendance", out var attendanceElement) && attendanceElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var att in attendanceElement.EnumerateObject())
                        {
                            if (att.Value.ValueKind == JsonValueKind.Array)
                            {
                                var list = new List<string>();
                                foreach (var item in att.Value.EnumerateArray())
                                    list.Add(item.GetString() ?? "");
                                attendanceDict[att.Name] = list;
                            }
                            else
                            {
                                attendanceDict[att.Name] = att.Value.GetRawText();
                            }
                        }
                    }
                }

                var payload = new Dictionary<string, object>
                {
                    ["homework"] = homeworkDict,
                    ["attendance"] = attendanceDict
                };

                var json = JsonSerializer.Serialize(payload, AppJsonSerializerContext.Default.DictionaryStringObject);
                var dateKey = $"classworks-data-{_currentDate:yyyyMMdd}";
                var response = await SendKvRequestAsync(HttpMethod.Post, $"/kv/{Uri.EscapeDataString(dateKey)}", json);

                if (response != null)
                {
                    _rawJson = json;
                    await LoadHomeworkAsync(_currentDate);
                }
            }
            catch (Exception)
            {
                StatusText.Text = "添加作业失败。";
            }
        }

        // ========== 随机抽取学生 ==========

        private async void PickRandomStudentButton_Click(object sender, RoutedEventArgs e)
        {
            var pickerWindow = new RandomPickerWindow();
            pickerWindow.Activate();
        }
    }
}
