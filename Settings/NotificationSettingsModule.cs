using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using System.Linq;

using CSD.Models;
using CSD.Services;
using CSD.Helpers;

namespace CSD.Settings
{
    public class NotificationSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "notification";
        public override string Title => "通知设置";
        public override string Description => "配置云端通知的提醒音效。";
        public override string Glyph => "\uEA8F";

        private ComboBox _normalSoundCombo = null!;
        private ComboBox _urgentSoundCombo = null!;

        private string[] GetSoundFiles()
        {
            var dir = Path.Combine(System.AppContext.BaseDirectory, "Assets", "sounds");
            if (Directory.Exists(dir))
            {
                return Directory.GetFiles(dir, "*.mp3").Select(Path.GetFileName).ToArray()!;
            }
            return new string[0];
        }

        protected override FrameworkElement BuildContent()
        {
            var sounds = GetSoundFiles();

            _normalSoundCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 40 };
            _urgentSoundCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 40 };

            _normalSoundCombo.Items.Add("无");
            _urgentSoundCombo.Items.Add("无");

            foreach (var sound in sounds)
            {
                _normalSoundCombo.Items.Add(sound);
                _urgentSoundCombo.Items.Add(sound);
            }

            var hintBar = new InfoBar
            {
                Title = "提示",
                Message = "由于系统权限限制，若希望紧急通知能够强制置顶在所有窗口最上层，建议以管理员身份运行此程序。",
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 16)
            };

            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(hintBar);
            panel.Children.Add(SettingsUIHelper.CreateSettingsGroup("音效",
                SettingsUIHelper.CreateSettingRow("常规通知音效", "收到普通通知时播放的声音。", new FontIcon { Glyph = "\uE8D6" }, _normalSoundCombo),
                SettingsUIHelper.CreateSettingRow("紧急通知音效", "收到紧急通知时循环播放的声音。", new FontIcon { Glyph = "\uEA8F" }, _urgentSoundCombo)));

            return panel;
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            var normalSound = settings["Settings_NormalNotificationSound"] as string ?? "Teams notification.mp3";
            var urgentSound = settings["Settings_UrgentNotificationSound"] as string ?? "Teams 警报.mp3";

            _normalSoundCombo.SelectedItem = _normalSoundCombo.Items.Contains(normalSound) ? normalSound : "无";
            _urgentSoundCombo.SelectedItem = _urgentSoundCombo.Items.Contains(urgentSound) ? urgentSound : "无";
        }

        protected override void HookAutoSaveHandlers()
        {
            _normalSoundCombo.SelectionChanged += (_, _) => 
            {
                NotifySettingsChanged();
                var selected = _normalSoundCombo.SelectedItem as string;
                if (selected != "无" && !string.IsNullOrEmpty(selected))
                {
                    SoundService.PlaySound(selected);
                }
            };
            
            _urgentSoundCombo.SelectionChanged += (_, _) => 
            {
                NotifySettingsChanged();
                var selected = _urgentSoundCombo.SelectedItem as string;
                if (selected != "无" && !string.IsNullOrEmpty(selected))
                {
                    SoundService.PlaySound(selected);
                }
            };
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;
            settings["Settings_NormalNotificationSound"] = _normalSoundCombo.SelectedItem as string ?? string.Empty;
            settings["Settings_UrgentNotificationSound"] = _urgentSoundCombo.SelectedItem as string ?? string.Empty;
        }
    }
}