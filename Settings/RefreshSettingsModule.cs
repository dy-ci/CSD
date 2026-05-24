using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class RefreshSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "refresh";
        public override string Title => "刷新设置";
        public override string Description => "定时从数据源拉取最新作业，并驱动主界面等全局组件一并更新。";
        public override string Glyph => "\uE72C";

        private ToggleSwitch _autoRefreshToggle = null!;
        private NumberBox _autoRefreshIntervalBox = null!;

        protected override FrameworkElement BuildContent()
        {
            var autoIcon = new ImageIcon
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(AppSettings.GetAssetUri("icons/ic_public_refresh.ico")),
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var intervalIcon = new ImageIcon
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(AppSettings.GetAssetUri("icons/ic_statusbar_alarm.ico")),
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _autoRefreshToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            _autoRefreshIntervalBox = SettingsUIHelper.CreateNumberBoxWithoutHeader(10, 600, 10, 60);

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("刷新策略",
                    SettingsUIHelper.CreateSettingRow("自动刷新", "定时从服务器获取最新作业数据。", autoIcon, _autoRefreshToggle),
                    SettingsUIHelper.CreateSettingRow("刷新间隔", "自动刷新的频率（秒）。", intervalIcon, _autoRefreshIntervalBox)));
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            _autoRefreshToggle.IsOn = settings.GetBool("Settings_AutoRefreshEnabled", false);
            _autoRefreshIntervalBox.Value = (double)(settings["Settings_AutoRefreshInterval"] ?? 60.0);
        }

        protected override void HookAutoSaveHandlers()
        {
            _autoRefreshToggle.Toggled += (_, _) => NotifySettingsChanged();
            _autoRefreshIntervalBox.ValueChanged += (_, _) => NotifySettingsChanged();
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;
            settings["Settings_AutoRefreshEnabled"] = _autoRefreshToggle.IsOn;
            settings["Settings_AutoRefreshInterval"] = _autoRefreshIntervalBox.Value;
        }
    }
}