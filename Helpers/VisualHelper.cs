using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CSD.Models;
using Microsoft.UI;
using Windows.UI;

namespace CSD.Helpers
{
    internal static class VisualHelper
    {
        public static void ApplyWindowBackdrop(Window window)
        {
            var enabled = AppSettings.Values.GetBool("Settings_BackgroundBlurEffects", true);
            
            if (enabled)
            {
                // Try to use Mica as default high-quality backdrop
                window.SystemBackdrop = new MicaBackdrop();
            }
            else
            {
                window.SystemBackdrop = null;
                // If we want a specific fallback color when blur is off, we could set it on the root content
                // But usually, the default theme background is fine.
            }
        }

        public static bool IsHighResResourceEnabled()
        {
            return AppSettings.Values.GetBool("Settings_HighResResourceLoading", true);
        }

        public static bool IsHighFramerateEnabled()
        {
            return AppSettings.Values.GetBool("Settings_HighFramerateRendering", true);
        }
    }
}
