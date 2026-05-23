using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Numerics;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Helpers
{
    internal static class AnimationHelper
    {
        public static void AnimateEntrance(UIElement element, float fromY = 20f, float fromOpacity = 0f, double durationMs = 360, double delayMs = 0)
        {
            var enabled = (bool)(AppSettings.Values["Settings_PageTransitionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);
            
            if (element.XamlRoot is null && element is FrameworkElement frameworkElement)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    AnimateEntrance(element, fromY, fromOpacity, durationMs, delayMs);
                };
                frameworkElement.Loaded += loadedHandler;
                return;
            }

            if (!enabled)
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                visual.Opacity = 1f;
                visual.Offset = Vector3.Zero;
                return;
            }

            if (!highFramerate) durationMs = 0;
            if (durationMs < 1) durationMs = 1; // Minimum duration for Composition animations

            RunEntranceAnimation(element, fromY, fromOpacity, durationMs, delayMs);
        }

        public static void AttachHoverAnimation(UIElement element, float hoverScale = 1.02f, float pressedScale = 0.985f, float hoverOffsetY = -4f, bool enablePressedFeedback = true)
        {
            EnsureCenterPoint(element);

            element.PointerEntered += (_, _) => AnimateInteraction(element, hoverScale, 180);
            element.PointerExited += (_, _) => AnimateInteraction(element, 1f, 180);
            element.PointerCanceled += (_, _) => AnimateInteraction(element, 1f, 180);
            element.PointerCaptureLost += (_, _) => AnimateInteraction(element, 1f, 180);

            if (enablePressedFeedback)
            {
                element.PointerPressed += (_, _) => AnimateInteraction(element, pressedScale, 90);
                element.PointerReleased += (_, _) => AnimateInteraction(element, hoverScale, 140);
            }
        }

        public static void AnimateOpacity(UIElement element, float fromOpacity, float toOpacity, double durationMs = 220)
        {
            var enabled = (bool)(AppSettings.Values["Settings_PageTransitionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);

            if (!enabled)
            {
                var visualOpacity = ElementCompositionPreview.GetElementVisual(element);
                visualOpacity.Opacity = toOpacity;
                return;
            }

            if (!highFramerate) durationMs = 0;
            if (durationMs < 1) durationMs = 1;

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            visual.Opacity = fromOpacity;

            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(1f, toOpacity);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            opacityAnimation.Target = "Opacity";

            visual.StartAnimation("Opacity", opacityAnimation);
        }

        public static void AnimateToOpacity(UIElement element, float toOpacity, double durationMs = 220)
        {
            var enabled = (bool)(AppSettings.Values["Settings_PageTransitionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);

            if (!enabled)
            {
                var visualOpacity = ElementCompositionPreview.GetElementVisual(element);
                visualOpacity.Opacity = toOpacity;
                return;
            }

            if (!highFramerate) durationMs = 0;
            if (durationMs < 1) durationMs = 1;

            if (element.XamlRoot is null && element is FrameworkElement frameworkElement)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    AnimateToOpacity(element, toOpacity, durationMs);
                };
                frameworkElement.Loaded += loadedHandler;
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(1f, toOpacity, easing);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            opacityAnimation.Target = "Opacity";

            visual.StartAnimation("Opacity", opacityAnimation);
        }

        public static void AnimateOffsetY(UIElement element, float toY, double durationMs = 240, float overshoot = 4f)
        {
            var enabled = (bool)(AppSettings.Values["Settings_PageTransitionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);

            if (!enabled)
            {
                var visualOffset = ElementCompositionPreview.GetElementVisual(element);
                visualOffset.Offset = new Vector3(visualOffset.Offset.X, toY, visualOffset.Offset.Z);
                return;
            }

            if (!highFramerate) durationMs = 0;
            if (durationMs < 1) durationMs = 1;

            if (element.XamlRoot is null && element is FrameworkElement frameworkElement)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    AnimateOffsetY(element, toY, durationMs, overshoot);
                };
                frameworkElement.Loaded += loadedHandler;
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var currentOffset = visual.Offset;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.18f, 0.95f), new Vector2(0.24f, 1f));

            var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
            if (Math.Abs(toY - currentOffset.Y) > 0.5f && durationMs > 1)
            {
                float overshootY = toY + (toY > currentOffset.Y ? overshoot : -overshoot);
                offsetAnimation.InsertKeyFrame(0.82f, new Vector3(currentOffset.X, overshootY, currentOffset.Z), easing);
            }
            offsetAnimation.InsertKeyFrame(1f, new Vector3(currentOffset.X, toY, currentOffset.Z), easing);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            offsetAnimation.Target = "Offset";

            visual.StartAnimation("Offset", offsetAnimation);
        }

        public static void AnimateScaleTo(UIElement element, float toScale, double durationMs = 220, float overshoot = 0.02f)
        {
            var enabled = (bool)(AppSettings.Values["Settings_PageTransitionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);

            if (!enabled)
            {
                var visualScale = ElementCompositionPreview.GetElementVisual(element);
                visualScale.Scale = new Vector3(toScale, toScale, 1f);
                return;
            }

            if (!highFramerate) durationMs = 0;
            if (durationMs < 1) durationMs = 1;

            if (element.XamlRoot is null && element is FrameworkElement frameworkElement)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    AnimateScaleTo(element, toScale, durationMs, overshoot);
                };
                frameworkElement.Loaded += loadedHandler;
                return;
            }

            EnsureCenterPoint(element);

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var currentScale = visual.Scale;
            if (currentScale == Vector3.Zero)
            {
                currentScale = new Vector3(1f, 1f, 1f);
                visual.Scale = currentScale;
            }

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.18f, 0.95f), new Vector2(0.24f, 1f));
            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            if (overshoot > 0 && durationMs > 1)
            {
                scaleAnimation.InsertKeyFrame(0.7f, new Vector3(toScale + overshoot, toScale + overshoot, 1f), easing);
            }
            scaleAnimation.InsertKeyFrame(1f, new Vector3(toScale, toScale, 1f), easing);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            scaleAnimation.Target = "Scale";

            visual.StartAnimation("Scale", scaleAnimation);
        }

        public static void AnimateBrushColor(SolidColorBrush brush, Windows.UI.Color toColor, double durationMs = 220)
        {
            var enabled = (bool)(AppSettings.Values["Settings_ElementInteractionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);

            if (!enabled)
            {
                brush.Color = toColor;
                return;
            }

            if (!highFramerate) durationMs = 0;
            if (durationMs < 1) durationMs = 1;

            try
            {
                var animation = new ColorAnimation
                {
                    To = toColor,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EnableDependentAnimation = true,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(animation);
                Storyboard.SetTarget(animation, brush);
                Storyboard.SetTargetProperty(animation, "Color");
                storyboard.Begin();
            }
            catch
            {
                // Visual tree not ready (e.g. window not yet activated) — fall back to instant set
                brush.Color = toColor;
            }
        }

        public static void ApplyStandardInteractions(DependencyObject root)
        {
            if (root is FrameworkElement { Tag: "DisableHoverAnimation" })
            {
                return;
            }

            if (root is Button button)
            {
                AttachHoverAnimation(button, 1.02f, 0.985f, -2f);
                return;
            }
            else if (root is TextBox textBox)
            {
                AttachHoverAnimation(textBox, 1.005f, 1f, -1f);
                return;
            }
            else if (root is PasswordBox passwordBox)
            {
                AttachHoverAnimation(passwordBox, 1.005f, 1f, -1f);
                return;
            }
            else if (root is NumberBox numberBox)
            {
                AttachHoverAnimation(numberBox, 1.005f, 1f, -1f);
                return;
            }
            else if (root is ToggleSwitch toggleSwitch)
            {
                AttachHoverAnimation(toggleSwitch, 1.01f, 1f, -1f);
                return;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                ApplyStandardInteractions(VisualTreeHelper.GetChild(root, i));
            }
        }

        private static void RunEntranceAnimation(UIElement element, float fromY, float fromOpacity, double durationMs, double delayMs)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            visual.Opacity = fromOpacity;
            visual.Offset = new Vector3(0, fromY, 0);

            var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, Vector3.Zero);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(delayMs);
            offsetAnimation.Target = "Offset";

            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(1f, 1f);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            opacityAnimation.DelayTime = TimeSpan.FromMilliseconds(delayMs);
            opacityAnimation.Target = "Opacity";

            visual.StartAnimation("Offset", offsetAnimation);
            visual.StartAnimation("Opacity", opacityAnimation);
        }

        private static void AnimateInteraction(UIElement element, float scale, double durationMs)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var enabled = (bool)(AppSettings.Values["Settings_ElementInteractionAnimations"] ?? true);
            var highFramerate = (bool)(AppSettings.Values["Settings_HighFramerateRendering"] ?? true);
            
            if (!enabled)
            {
                visual.Scale = new Vector3(1f, 1f, 1f);
                return;
            }

            if (!highFramerate) durationMs = 0; // Instant if low framerate requested
            if (durationMs < 1) durationMs = 1;

            var compositor = visual.Compositor;

            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, new Vector3(scale, scale, 1f));
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
            scaleAnimation.Target = "Scale";

            visual.StartAnimation("Scale", scaleAnimation);
        }

        private static void EnsureCenterPoint(UIElement element)
        {
            if (element is not FrameworkElement frameworkElement)
            {
                return;
            }

            void updateCenterPoint()
            {
                var visual = ElementCompositionPreview.GetElementVisual(frameworkElement);
                visual.CenterPoint = new Vector3(
                    (float)Math.Max(0, frameworkElement.ActualWidth / 2),
                    (float)Math.Max(0, frameworkElement.ActualHeight / 2),
                    0);
            }

            frameworkElement.Loaded += (_, _) => updateCenterPoint();
            frameworkElement.SizeChanged += (_, _) => updateCenterPoint();
        }
    }
}
