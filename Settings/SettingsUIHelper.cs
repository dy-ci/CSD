using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public static class SettingsUIHelper
    {
        public static StackPanel CreateCategoryView(params UIElement[] sections)
        {
            var panel = new StackPanel
            {
                Spacing = 16,
                Visibility = Visibility.Collapsed
            };

            foreach (var section in sections)
            {
                panel.Children.Add(section);
            }

            return panel;
        }

        public static Color GetBrushColor(string resourceKey, Color fallbackColor)
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return fallbackColor;
        }

        public static Color WithAlpha(Color color, byte alpha)
        {
            return ColorHelper.FromArgb(alpha, color.R, color.G, color.B);
        }

        public static Border CreateSettingRow(string title, string description, FrameworkElement? icon = null, FrameworkElement? control = null)
        {
            var iconHost = new Grid { Width = 24, Height = 24, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            if (icon != null)
            {
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                icon.VerticalAlignment = VerticalAlignment.Center;
                iconHost.Children.Add(icon);
            }
            else
            {
                iconHost.Visibility = Visibility.Collapsed;
            }

            var labelStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            labelStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium
            });
            if (!string.IsNullOrEmpty(description))
            {
                labelStack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            var contentGrid = new Grid { ColumnSpacing = 0 };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(iconHost, 0);
            Grid.SetColumn(labelStack, 1);
            contentGrid.Children.Add(iconHost);
            contentGrid.Children.Add(labelStack);

            if (control != null)
            {
                control.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 2);
                contentGrid.Children.Add(control);
            }

            return new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                Child = contentGrid
            };
        }

        public static Border CreateCompoundSettingRow(string title, string description, FrameworkElement control, FrameworkElement? icon = null)
        {
            var iconHost = new Grid { Width = 24, Height = 24, Margin = new Thickness(0, 4, 12, 0), VerticalAlignment = VerticalAlignment.Top };
            if (icon != null)
            {
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                icon.VerticalAlignment = VerticalAlignment.Center;
                iconHost.Children.Add(icon);
            }
            else
            {
                iconHost.Visibility = Visibility.Collapsed;
            }

            var labelStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Top };
            labelStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium
            });
            if (!string.IsNullOrEmpty(description))
            {
                labelStack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            var headerGrid = new Grid { ColumnSpacing = 0 };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(iconHost, 0);
            Grid.SetColumn(labelStack, 1);
            headerGrid.Children.Add(iconHost);
            headerGrid.Children.Add(labelStack);

            var contentStack = new StackPanel { Spacing = 8 };
            contentStack.Children.Add(headerGrid);
            contentStack.Children.Add(control);

            return new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                Child = contentStack
            };
        }

        public static Border CreateSettingsGroup(string? title, params UIElement[] rows)
        {
            var stack = new StackPanel { Spacing = 0 };
            
            if (!string.IsNullOrEmpty(title))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(4, 0, 0, 8),
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            var cardStack = new StackPanel { Spacing = 0 };
            for (int i = 0; i < rows.Length; i++)
            {
                cardStack.Children.Add(rows[i]);
                if (i < rows.Length - 1)
                {
                    cardStack.Children.Add(new Border
                    {
                        Height = 1,
                        Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                        Margin = new Thickness(16, 0, 16, 0)
                    });
                }
            }

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = cardStack
            };

            stack.Children.Add(card);
            return new Border { Child = stack, Margin = new Thickness(0, 0, 0, 16) };
        }

        public static NumberBox CreateNumberBoxWithoutHeader(double minimum, double maximum, double step, double defaultValue)
        {
            var box = new NumberBox
            {
                Minimum = minimum,
                Maximum = maximum,
                SmallChange = step,
                LargeChange = step * 5,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Value = defaultValue,
                MinWidth = 100,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            TouchKeyboardHelper.EnableForControl(box);
            return box;
        }

        public static Border CreateSectionCard(string title, string description, params UIElement[] children)
        {
            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            foreach (var child in children)
            {
                stack.Children.Add(child);
            }

            return new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Child = stack
            };
        }

        public static StackPanel CreateIconTextRow(string glyph, string text)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            });
            return row;
        }

        public static Border CreateFilledCard(UIElement inner)
        {
            return new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18, 16, 18, 16),
                Child = inner
            };
        }

        public static TextBlock CreateSecondaryWrappedText(string text, double fontSize = 14)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
        }
    }
}
