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
            
            int width = 450;
            int height = 250;

            if (displayArea != null)
            {
                if (isUrgent)
                {
                    // 对于紧急通知，增大窗口尺寸
                    width = (int)(displayArea.WorkArea.Width * 0.5);
                    height = (int)(displayArea.WorkArea.Height * 0.4);
                    
                    // 设置最小尺寸
                    if (width < 600) width = 600;
                    if (height < 350) height = 350;
                }
                else
                {
                    // 对于普通通知，稍微增大一点窗口以容纳更大的字体
                    width = (int)(displayArea.WorkArea.Width * 0.35);
                    height = (int)(displayArea.WorkArea.Height * 0.25);
                    
                    if (width < 450) width = 450;
                    if (height < 250) height = 250;
                }
                
                // 无论是普通通知还是紧急通知，都根据屏幕高度自适应字体大小
                // 紧急通知字体稍微更大一点，普通通知略小一点
                double ratio = isUrgent ? 0.05 : 0.035;
                double adaptiveFontSize = displayArea.WorkArea.Height * ratio; 
                
                // 设置普通和紧急通知的字体大小上下限
                double minFont = isUrgent ? 28 : 20;
                double maxFont = isUrgent ? 80 : 50;
                
                if (adaptiveFontSize < minFont) adaptiveFontSize = minFont;
                if (adaptiveFontSize > maxFont) adaptiveFontSize = maxFont;
                
                MessageText.FontSize = adaptiveFontSize;
                // 恢复标题栏字体大小默认设置
                // TitleText.FontSize = adaptiveFontSize * 0.6;
            }

            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                var x = workArea.X + (workArea.Width - width) / 2;
                var y = workArea.Y + (workArea.Height - height) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
            }
            else
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
            }

            if (isUrgent)
            {
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                }
                
                // 移除系统背景材质以显示纯色
                this.SystemBackdrop = null;
                
                // 设置背景为红色 #F44336
                RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 67, 54));
                
                // 设置文字为白色
                TitleText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                MessageText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                
                // 调整按钮样式使其在红色背景上更清晰
                CloseButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 67, 54));
                CloseButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
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