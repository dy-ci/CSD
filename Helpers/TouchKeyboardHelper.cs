using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI.ViewManagement;

namespace CSD.Helpers
{
    internal static class TouchKeyboardHelper
    {
        public static void EnableForControl(Control control)
        {
            if (control == null) return;

            control.GotFocus += OnControlGotFocus;
        }

        private static void OnControlGotFocus(object sender, RoutedEventArgs e)
        {
            TryShowTouchKeyboard();
        }

        public static void TryShowTouchKeyboard()
        {
            try
            {
                var inputPane = InputPane.GetForCurrentView();
                if (inputPane != null)
                {
                    inputPane.TryShow();
                }
            }
            catch
            {
            }
        }

        public static void EnableForVisualTree(DependencyObject root)
        {
            if (root is Control control)
            {
                if (control is TextBox || control is PasswordBox || control is NumberBox ||
                    control is RichEditBox || control is AutoSuggestBox)
                {
                    EnableForControl(control);
                    return;
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                EnableForVisualTree(VisualTreeHelper.GetChild(root, i));
            }
        }
    }
}
