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
    public class RandomPickerSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "randomPicker";
        public override string Title => "随机点名";
        public override string Description => "配置随机点名功能的模式、范围和默认参数。";
        public override string Glyph => "\uE716";

        private ToggleSwitch _randomPickerEnabledToggle = null!;
        private NumberBox _randomPickerMinNumberBox = null!;
        private NumberBox _randomPickerMaxNumberBox = null!;
        private NumberBox _randomPickerDefaultCountBox = null!;
        private ToggleSwitch _randomPickerAnimationToggle = null!;

        protected override FrameworkElement BuildContent()
        {
            _randomPickerEnabledToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            _randomPickerMinNumberBox = SettingsUIHelper.CreateNumberBoxWithoutHeader(1, 999, 1, 1.0);
            _randomPickerMaxNumberBox = SettingsUIHelper.CreateNumberBoxWithoutHeader(1, 9999, 1, 60.0);
            _randomPickerDefaultCountBox = SettingsUIHelper.CreateNumberBoxWithoutHeader(1, 100, 1, 1.0);
            _randomPickerAnimationToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("常规",
                    SettingsUIHelper.CreateSettingRow("启用功能", "开启或关闭点名器入口。", new FontIcon { Glyph = "\uE73E" }, _randomPickerEnabledToggle),
                    SettingsUIHelper.CreateSettingRow("动画效果", "点名时是否播放滚动动画。", new FontIcon { Glyph = "\uE916" }, _randomPickerAnimationToggle)),
                SettingsUIHelper.CreateSettingsGroup("默认参数",
                    SettingsUIHelper.CreateSettingRow("学号最小值", "学号模式下抽取的下限。", new FontIcon { Glyph = "\uE8EF" }, _randomPickerMinNumberBox),
                    SettingsUIHelper.CreateSettingRow("学号最大值", "学号模式下抽取的上限。", new FontIcon { Glyph = "\uE8EF" }, _randomPickerMaxNumberBox),
                    SettingsUIHelper.CreateSettingRow("默认人数", "窗口打开时预设的抽取人数。", new FontIcon { Glyph = "\uE716" }, _randomPickerDefaultCountBox)));
        }

        private static double ConvertToDouble(object? value, double defaultValue)
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is float f) return f;
            return defaultValue;
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            _randomPickerEnabledToggle.IsOn = settings.GetBool("randomPicker.enabled", true);
            _randomPickerMinNumberBox.Value = ConvertToDouble(settings["randomPicker.minNumber"], 1.0);
            _randomPickerMaxNumberBox.Value = ConvertToDouble(settings["randomPicker.maxNumber"], 60.0);
            _randomPickerDefaultCountBox.Value = ConvertToDouble(settings["randomPicker.defaultCount"], 1.0);
            _randomPickerAnimationToggle.IsOn = settings.GetBool("randomPicker.animation", true);
        }

        protected override void HookAutoSaveHandlers()
        {
            _randomPickerEnabledToggle.Toggled += async (_, _) =>
            {
                if (!_randomPickerEnabledToggle.IsOn &&
                    !AppSettings.Values.GetBool("randomPicker.disableNotificationShown", false))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "随机点名已关闭",
                        Content = "随机点名功能已关闭，主界面上的\"随机抽取\"按钮将不再显示。如需重新开启，请再次进入设置。",
                        CloseButtonText = "确定",
                        XamlRoot = Context.Window.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                    AppSettings.Values["randomPicker.disableNotificationShown"] = true;
                }
                NotifySettingsChanged();
            };
            _randomPickerMinNumberBox.ValueChanged += (_, _) => NotifySettingsChanged();
            _randomPickerMaxNumberBox.ValueChanged += (_, _) => NotifySettingsChanged();
            _randomPickerDefaultCountBox.ValueChanged += (_, _) => NotifySettingsChanged();
            _randomPickerAnimationToggle.Toggled += (_, _) => NotifySettingsChanged();
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;
            settings["randomPicker.enabled"] = _randomPickerEnabledToggle.IsOn;
            settings["randomPicker.minNumber"] = (int)_randomPickerMinNumberBox.Value;
            settings["randomPicker.maxNumber"] = (int)_randomPickerMaxNumberBox.Value;
            settings["randomPicker.defaultCount"] = (int)_randomPickerDefaultCountBox.Value;
            settings["randomPicker.animation"] = _randomPickerAnimationToggle.IsOn;
        }
    }
}