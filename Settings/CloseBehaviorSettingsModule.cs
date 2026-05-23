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
    public class CloseBehaviorSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "closebehavior";
        public override string Title => "关闭行为";
        public override string Description => "设置关闭主窗口时的行为。";
        public override string Glyph => "\uE15D";

        private ComboBox _closeBehaviorCombo = null!;

        private static readonly string[] CloseBehaviorOptions =
        [
            "关闭软件",
            "隐藏到系统托盘"
        ];

        protected override FrameworkElement BuildContent()
        {
            _closeBehaviorCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                MinHeight = 32,
                MinWidth = 200
            };
            foreach (var label in CloseBehaviorOptions)
                _closeBehaviorCombo.Items.Add(label);

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("系统行为",
                    SettingsUIHelper.CreateSettingRow("关闭主窗口时", "选择点击关闭按钮后的行为", new FontIcon { Glyph = "\uE15D" }, _closeBehaviorCombo)));
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            var savedBehavior = settings["Settings_CloseBehavior"] as string ?? "Close";
            _closeBehaviorCombo.SelectedIndex = savedBehavior == "MinimizeToTray" ? 1 : 0;
        }

        protected override void HookAutoSaveHandlers()
        {
            _closeBehaviorCombo.SelectionChanged += (_, _) => NotifySettingsChanged();
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;

            if (_closeBehaviorCombo.SelectedIndex == 1)
            {
                settings["Settings_CloseBehavior"] = "MinimizeToTray";
                settings["Settings_FirstCloseDialogShown"] = true;
            }
            else
            {
                settings["Settings_CloseBehavior"] = "Close";
                settings["Settings_FirstCloseDialogShown"] = true;
            }
        }
    }
}
