using H.NotifyIcon;
using H.NotifyIcon.Interop;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Windows.UI;
using WinRT.Interop;

using CSD.Helpers;
using CSD.Models;

namespace CSD.Services
{
    public class TrayService : IDisposable
    {
        private readonly Window _window;
        private readonly IntPtr _hwnd;
        private TaskbarIcon? _taskbarIcon;
        private bool _disposed;

        public TrayService(Window window)
        {
            _window = window;
            _hwnd = WindowNative.GetWindowHandle(window);
        }

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon();

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                try
                {
                    _taskbarIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                catch { }
            }

            _taskbarIcon.ToolTipText = "CSD - Classworks Desktop";
            _taskbarIcon.PopupActivation = PopupActivationMode.RightClick;
            _taskbarIcon.TrayPopup = BuildPopupContent();
            _taskbarIcon.LeftClickCommand = new RelayCommand(ShowWindow);
        }

        public void ShowWindow()
        {
            if (_disposed) return;
            ShowWindow(_hwnd, SW_RESTORE);
            ShowWindow(_hwnd, SW_SHOW);
            SetForegroundWindow(_hwnd);
        }

        public void HideWindow()
        {
            if (_disposed) return;
            SaveWindowState();
            ShowWindow(_hwnd, SW_HIDE);
        }

        private async void Quit()
        {
            Dispose();
            await SocketIoService.Instance.DisposeAsync();
            Application.Current.Exit();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _taskbarIcon?.Dispose();
            _taskbarIcon = null;
        }

        private void SaveWindowState()
        {
            try
            {
                var settings = AppSettings.Values;
                settings["MainWindow_X"] = (double)_window.AppWindow.Position.X;
                settings["MainWindow_Y"] = (double)_window.AppWindow.Position.Y;
                settings["MainWindow_Width"] = (double)_window.AppWindow.Size.Width;
                settings["MainWindow_Height"] = (double)_window.AppWindow.Size.Height;

                if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    settings["MainWindow_State"] = presenter.State.ToString();
                }
            }
            catch { }
        }

        private FrameworkElement BuildPopupContent()
        {
            var bgBrush = GetBrush("CardBackgroundFillColorDefaultBrush", Color.FromArgb(255, 32, 32, 32));
            var borderBrush = GetBrush("CardStrokeColorDefaultBrush", Color.FromArgb(40, 255, 255, 255));
            var dividerBrush = GetBrush("DividerStrokeColorDefaultBrush", Color.FromArgb(24, 255, 255, 255));

            var stack = new StackPanel { Spacing = 2 };

            stack.Children.Add(CreateMenuItem("\uE8A7", "显示窗口", ShowWindow));
            stack.Children.Add(CreateMenuItem("\uE721", "隐藏窗口", HideWindow));

            stack.Children.Add(new Border
            {
                Height = 1,
                Background = dividerBrush,
                Margin = new Thickness(8, 4, 8, 4)
            });

            stack.Children.Add(CreateMenuItem("\uE711", "退出", Quit));

            return new Border
            {
                Width = 220,
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Child = stack
            };
        }

        private Border CreateMenuItem(string glyph, string text, Action onClick)
        {
            var secondaryText = GetBrush("TextFillColorSecondaryBrush", Color.FromArgb(179, 255, 255, 255));
            var primaryText = GetBrush("TextFillColorPrimaryBrush", Color.FromArgb(242, 255, 255, 255));
            var hoverColor = Color.FromArgb(26, 255, 255, 255);
            var isExit = glyph == "\uE711";

            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 14,
                Foreground = secondaryText,
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = isExit ? new SolidColorBrush(Color.FromArgb(255, 255, 100, 100)) : primaryText,
                VerticalAlignment = VerticalAlignment.Center
            };

            var contentGrid = new Grid { ColumnSpacing = 10 };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(label, 1);
            contentGrid.Children.Add(icon);
            contentGrid.Children.Add(label);

            var hoverOverlay = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(hoverColor),
                Opacity = 0,
                IsHitTestVisible = false
            };

            var root = new Grid();
            root.Children.Add(hoverOverlay);
            root.Children.Add(contentGrid);
            Grid.SetColumnSpan(hoverOverlay, 2);

            var container = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                Child = root,
                CornerRadius = new CornerRadius(6)
            };

            container.PointerEntered += (_, _) => AnimationHelper.AnimateToOpacity(hoverOverlay, 1, 120);
            container.PointerExited += (_, _) => AnimationHelper.AnimateToOpacity(hoverOverlay, 0, 120);
            container.PointerCanceled += (_, _) => AnimationHelper.AnimateToOpacity(hoverOverlay, 0, 120);
            container.PointerCaptureLost += (_, _) => AnimationHelper.AnimateToOpacity(hoverOverlay, 0, 120);

            container.Tapped += (_, _) => onClick();

            return container;
        }

        private static Brush GetBrush(string resourceKey, Color fallbackColor)
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is Brush brush)
                return brush;
            return new SolidColorBrush(fallbackColor);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
    }

    internal sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
