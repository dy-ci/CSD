using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage.Pickers;
using WinRT.Interop;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class AccountSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "account";
        public override string Title => "账户与数据";
        public override string Description => "查看 Token 状态，并进行本地设置导入导出。";
        public override string Glyph => "\uE716";

        private TextBlock _currentTokenText = null!;
        private PasswordBox _tokenInputBox = null!;

        protected override FrameworkElement BuildContent()
        {
            _currentTokenText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            var destroyTokenButton = new Button { Content = "销毁 Token", HorizontalAlignment = HorizontalAlignment.Right };
            destroyTokenButton.Click += DestroyTokenButton_Click;

            var tokenStatusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
            tokenStatusRow.Children.Add(_currentTokenText);
            tokenStatusRow.Children.Add(destroyTokenButton);

            _tokenInputBox = new PasswordBox
            {
                PlaceholderText = "输入 KV 授权令牌",
                Width = 240
            };
            TouchKeyboardHelper.EnableForControl(_tokenInputBox);

            var applyTokenButton = new Button { Content = "应用", Width = 80 };
            applyTokenButton.Click += ApplyTokenButton_Click;

            var tokenInputRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            tokenInputRow.Children.Add(_tokenInputBox);
            tokenInputRow.Children.Add(applyTokenButton);

            var exportButton = new Button { Content = "导出设置", HorizontalAlignment = HorizontalAlignment.Stretch };
            exportButton.Click += ExportButton_Click;

            var importButton = new Button { Content = "导入设置", HorizontalAlignment = HorizontalAlignment.Stretch };
            importButton.Click += ImportButton_Click;

            var webSettingsButton = new Button { Content = "网页端设置", HorizontalAlignment = HorizontalAlignment.Stretch };
            webSettingsButton.Click += (_, _) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://cs.houlang.cloud/settings", UseShellExecute = true });
                }
                catch { }
            };

            var ioStack = new Grid { ColumnSpacing = 8 };
            ioStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ioStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ioStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(exportButton, 0);
            Grid.SetColumn(importButton, 1);
            Grid.SetColumn(webSettingsButton, 2);
            ioStack.Children.Add(exportButton);
            ioStack.Children.Add(importButton);
            ioStack.Children.Add(webSettingsButton);

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("账户",
                    SettingsUIHelper.CreateSettingRow("当前状态", "查看授权状态并可重置。", new FontIcon { Glyph = "\uE77B" }, tokenStatusRow),
                    SettingsUIHelper.CreateSettingRow("输入令牌", "手动输入新的 KV 授权令牌。", new FontIcon { Glyph = "\uE8D7" }, tokenInputRow)),
                SettingsUIHelper.CreateSettingsGroup("数据管理",
                    SettingsUIHelper.CreateCompoundSettingRow("本地与云端", "导出、导入本地设置，或前往网页端进行管理。", ioStack, new FontIcon { Glyph = "\uE8B5" })));
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            var token = settings["Token"] as string ?? "";
            _currentTokenText.Text = string.IsNullOrWhiteSpace(token) ? "未设置" : "已设置";
        }

        private async void DestroyTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "确认销毁 Token",
                Content = "是否重新初始化并重启应用？",
                PrimaryButtonText = "是",
                CloseButtonText = "否",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Context.Window.Content.XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            AppSettings.Values.Remove("Token");
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                System.Diagnostics.Process.Start(exePath);
            Application.Current.Exit();
        }

        private async void ApplyTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_tokenInputBox.Password))
                return;

            AppSettings.Values["Token"] = _tokenInputBox.Password;
            _currentTokenText.Text = "已设置";
            _tokenInputBox.Password = string.Empty;

            var dialog = new ContentDialog
            {
                Title = "Token 已更新",
                Content = "Token 已成功保存。是否重新启动应用？",
                PrimaryButtonText = "重新启动",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Context.Window.Content.XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    System.Diagnostics.Process.Start(exePath);
                Application.Current.Exit();
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "CSD设置"
            };
            savePicker.FileTypeChoices.Add("JSON 文件", new List<string> { ".json" });
            InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(Context.Window));

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                var settings = AppSettings.Values;
                var data = new Dictionary<string, object>();
                foreach (var kvp in settings)
                    data[kvp.Key] = kvp.Value ?? "";
                var json = JsonSerializer.Serialize(data, AppJsonIndentedSerializerContext.Default.DictionaryStringObject);
                await Windows.Storage.FileIO.WriteTextAsync(file, json);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            openPicker.FileTypeFilter.Add(".json");
            InitializeWithWindow.Initialize(openPicker, WindowNative.GetWindowHandle(Context.Window));

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                    var data = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringJsonElement);
                    if (data == null) return;

                    var settings = AppSettings.Values;
                    foreach (var kvp in data)
                    {
                        if (kvp.Value.ValueKind == JsonValueKind.String)
                            settings[kvp.Key] = kvp.Value.GetString() ?? string.Empty;
                        else if (kvp.Value.ValueKind == JsonValueKind.Number)
                            settings[kvp.Key] = kvp.Value.GetDouble();
                        else if (kvp.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                            settings[kvp.Key] = kvp.Value.GetBoolean();
                    }

                    NotifySettingsChanged();
                }
                catch (Exception ex)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "导入失败",
                        Content = ex.Message,
                        CloseButtonText = "确定",
                        XamlRoot = Context.Window.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
        }
    }
}