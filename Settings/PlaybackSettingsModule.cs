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
    public class PlaybackSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "playback";
        public override string Title => "轮播与调试";
        public override string Description => "控制课堂展示轮播效果，并按需开启调试模式。";
        public override string Glyph => "\uE8B2";

        private NumberBox _carouselIntervalBox = null!;
        private NumberBox _carouselFontSizeBox = null!;
        private ToggleSwitch _debugModeToggle = null!;

        protected override FrameworkElement BuildContent()
        {
            _carouselIntervalBox = SettingsUIHelper.CreateNumberBoxWithoutHeader(1, 120, 1, 5);
            _carouselFontSizeBox = SettingsUIHelper.CreateNumberBoxWithoutHeader(16, 120, 4, 48);
            _debugModeToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0 };

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("轮播",
                    SettingsUIHelper.CreateSettingRow("轮播切换间隔", "课堂展示时轮播的切换速度。", new FontIcon { Glyph = "\uE916" }, _carouselIntervalBox),
                    SettingsUIHelper.CreateSettingRow("轮播字体大小", "轮播模式下的展示字号。", new FontIcon { Glyph = "\uE8D2" }, _carouselFontSizeBox)),
                SettingsUIHelper.CreateSettingsGroup("高级",
                    SettingsUIHelper.CreateSettingRow("调试模式", "在需要排查问题时启用。", new FontIcon { Glyph = "\uEBE8" }, _debugModeToggle)));
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            _carouselIntervalBox.Value = (double)(settings["Settings_CarouselInterval"] ?? 5.0);
            _carouselFontSizeBox.Value = (double)(settings["Settings_CarouselFontSize"] ?? 48.0);
            _debugModeToggle.IsOn = settings.GetBool("Settings_DebugMode", false);
        }

        protected override void HookAutoSaveHandlers()
        {
            _carouselIntervalBox.ValueChanged += (_, _) => NotifySettingsChanged();
            _carouselFontSizeBox.ValueChanged += (_, _) => NotifySettingsChanged();
            _debugModeToggle.Toggled += (_, _) => NotifySettingsChanged();
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;
            settings["Settings_CarouselInterval"] = _carouselIntervalBox.Value;
            settings["Settings_CarouselFontSize"] = _carouselFontSizeBox.Value;
            settings["Settings_DebugMode"] = _debugModeToggle.IsOn;
        }
    }
}