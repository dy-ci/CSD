using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class AutoStartSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "autostart";
        public override string Title => "自启动";
        public override string Description => "配置开机自启动行为，选择不同的启动方式以实现不同程度的兼容性。";
        public override string Glyph => "\uE7E8";

        private ToggleSwitch _autoStartToggle = null!;
        private ComboBox _autoStartMethodCombo = null!;

        private static readonly string[] AutoStartMethodOptions =
        [
            "注册表 (Registry)  ★★★",
            "启动文件夹 (Startup)  ★★",
            "计划任务 (Task Scheduler)  ★"
        ];

        protected override FrameworkElement BuildContent()
        {
            _autoStartToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            
            _autoStartMethodCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                MinHeight = 32,
                MinWidth = 200
            };
            foreach (var label in AutoStartMethodOptions)
                _autoStartMethodCombo.Items.Add(label);

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("系统行为",
                    SettingsUIHelper.CreateSettingRow("开机自启动", "登录 Windows 时自动运行 CSD。", new FontIcon { Glyph = "\uE7E8" }, _autoStartToggle),
                    SettingsUIHelper.CreateSettingRow("启动方式", "星级越高越稳定推荐。", new FontIcon { Glyph = "\uE946" }, _autoStartMethodCombo)));
        }

        protected override void LoadSettings()
        {
            var settings = AppSettings.Values;
            _autoStartToggle.IsOn = settings.GetBool("Settings_AutoStartEnabled", false);

            var savedMethod = settings["Settings_AutoStartMethod"] as string;
            if (!string.IsNullOrWhiteSpace(savedMethod))
            {
                for (int i = 0; i < AutoStartMethodOptions.Length; i++)
                {
                    if (AutoStartMethodOptions[i].StartsWith(savedMethod, StringComparison.Ordinal))
                    {
                        _autoStartMethodCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (_autoStartMethodCombo.SelectedIndex < 0)
                _autoStartMethodCombo.SelectedIndex = 0;
        }

        protected override void HookAutoSaveHandlers()
        {
            _autoStartToggle.Toggled += (_, _) => { NotifySettingsChanged(); _ = ApplyAutoStartAsync(); };
            _autoStartMethodCombo.SelectionChanged += (_, _) => { NotifySettingsChanged(); _ = ApplyAutoStartAsync(); };
        }

        public override void PersistSettings()
        {
            var settings = AppSettings.Values;
            settings["Settings_AutoStartEnabled"] = _autoStartToggle.IsOn;
            
            if (_autoStartMethodCombo.SelectedItem is string methodLabel)
            {
                var methodName = methodLabel.Split("  ")[0];
                settings["Settings_AutoStartMethod"] = methodName;
            }
            else if (_autoStartMethodCombo.SelectedIndex >= 0 && _autoStartMethodCombo.SelectedIndex < AutoStartMethodOptions.Length)
            {
                var methodName = AutoStartMethodOptions[_autoStartMethodCombo.SelectedIndex].Split("  ")[0];
                settings["Settings_AutoStartMethod"] = methodName;
            }
        }

        private async System.Threading.Tasks.Task ApplyAutoStartAsync()
        {
            if (IsAutoSaveSuspended)
                return;

            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    RemoveAllAutoStartEntries();
                    if (_autoStartToggle.IsOn)
                        ApplySelectedAutoStartMethod();
                }
                catch
                {
                    // 静默处理自启动设置失败
                }
            });
        }

        private void RemoveAllAutoStartEntries()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            RemoveRegistryAutoStart();
            RemoveStartupFolderShortcut();
            RemoveScheduledTask();
        }

        private void ApplySelectedAutoStartMethod()
        {
            var methodIndex = _autoStartMethodCombo.SelectedIndex;
            if (methodIndex < 0 || methodIndex >= AutoStartMethodOptions.Length)
                methodIndex = 0;

            switch (methodIndex)
            {
                case 0:
                    SetRegistryAutoStart();
                    break;
                case 1:
                    SetStartupFolderAutoStart();
                    break;
                case 2:
                    SetScheduledTaskAutoStart();
                    break;
            }
        }

        private static void RemoveRegistryAutoStart()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                key?.DeleteValue("CSD", throwOnMissingValue: false);
            }
            catch { }
        }

        private static void SetRegistryAutoStart()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");
                key?.SetValue("CSD", $"\"{exePath}\"");
            }
            catch { }
        }

        private static void RemoveStartupFolderShortcut()
        {
            try
            {
                var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupPath, "CSD.lnk");
                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);
            }
            catch { }
        }

        private static void SetStartupFolderAutoStart()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            try
            {
                var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                Directory.CreateDirectory(startupPath);
                var shortcutPath = Path.Combine(startupPath, "CSD.lnk");

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "CSD - 课堂作业展示系统";
                shortcut.Save();
            }
            catch { }
        }

        private static void RemoveScheduledTask()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", "/delete /tn \"CSD_AutoStart\" /f")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(5000);
            }
            catch { }
        }

        private static void SetScheduledTaskAutoStart()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            try
            {
                var arguments = $"/create /tn \"CSD_AutoStart\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl limited /f /delay 0000:30";
                var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(5000);
            }
            catch { }
        }
    }
}