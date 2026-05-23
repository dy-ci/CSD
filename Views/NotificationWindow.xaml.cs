using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using CSD.Services;
using System;
using Microsoft.UI;

namespace CSD.Views
{
    public sealed partial class NotificationWindow : Window
    {
        private readonly bool _isUrgent;

        public NotificationWindow(string title, string message, bool isUrgent)
        {
            this.InitializeComponent();
            _isUrgent = isUrgent;
            
            TitleText.Text = title;
            MessageText.Text = message;
            CloseButton.Content = isUrgent ? "我知道了" : "关闭";

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);
            }

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                var x = workArea.X + (workArea.Width - 450) / 2;
                var y = workArea.Y + (workArea.Height - 250) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, 450, 250));
            }
            else
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 450, Height = 250 });
            }

            if (isUrgent)
            {
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                }
                
                // Set the title text color to Red for urgent notices
                TitleText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                MessageText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            SoundService.StopSound();
        }
    }
}