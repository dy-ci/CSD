using H.NotifyIcon;
using H.NotifyIcon.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using WinRT.Interop;

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
            _taskbarIcon.ContextFlyout = BuildContextMenu();
            _taskbarIcon.MenuActivation = PopupActivationMode.RightClick;
            _taskbarIcon.LeftClickCommand = new RelayCommand(ShowWindow);
        }

        private MenuFlyout BuildContextMenu()
        {
            var flyout = new MenuFlyout();

            var showItem = new MenuFlyoutItem
            {
                Icon = new SymbolIcon(Symbol.Home),
                Text = "显示窗口",
                Command = new RelayCommand(ShowWindow)
            };
            flyout.Items.Add(showItem);

            var hideItem = new MenuFlyoutItem
            {
                Icon = new FontIcon { Glyph = "\uE8A7" },
                Text = "隐藏窗口",
                Command = new RelayCommand(HideWindow)
            };
            flyout.Items.Add(hideItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var quitItem = new MenuFlyoutItem
            {
                Icon = new SymbolIcon(Symbol.Cancel),
                Text = "退出",
                Command = new RelayCommand(Quit)
            };
            flyout.Items.Add(quitItem);

            return flyout;
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
