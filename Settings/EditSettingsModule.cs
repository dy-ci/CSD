using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class EditSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "edit";
        public override string Title => "编辑设置";
        public override string Description => "自动保存、非当天写入限制、确认与提示文案；部分项会同步到云端。";
        public override string Glyph => "\uE70F";

        private ToggleSwitch _editAutoSaveToggle = null!;
        private ToggleSwitch _editBlockNonTodayAutoSaveToggle = null!;
        private ToggleSwitch _editConfirmNonTodaySaveToggle = null!;
        private ToggleSwitch _editRefreshBeforeEditToggle = null!;
        private TextBox _editAutoSavePromptTextBox = null!;
        private TextBox _editManualSavePromptTextBox = null!;
        
        private int _editCloudPushGeneration;
        private bool _editPrefsPulledThisSession;

        protected override FrameworkElement BuildContent()
        {
            _editAutoSaveToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            _editBlockNonTodayAutoSaveToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            _editConfirmNonTodaySaveToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            _editRefreshBeforeEditToggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0, Margin = new Thickness(0) };
            _editAutoSavePromptTextBox = new TextBox { AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
            _editManualSavePromptTextBox = new TextBox { AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
            TouchKeyboardHelper.EnableForControl(_editAutoSavePromptTextBox);
            TouchKeyboardHelper.EnableForControl(_editManualSavePromptTextBox);

            return SettingsUIHelper.CreateCategoryView(
                SettingsUIHelper.CreateSettingsGroup("常规",
                    SettingsUIHelper.CreateSettingRow("自动保存", "编辑作业后是否自动同步到云端。", new FontIcon { Glyph = "\uE74E" }, _editAutoSaveToggle),
                    SettingsUIHelper.CreateSettingRow("限制非当天写入", "禁止写入非当天作业数据，防止误操作。", new FontIcon { Glyph = "\uE787" }, _editBlockNonTodayAutoSaveToggle),
                    SettingsUIHelper.CreateSettingRow("保存确认", "保存非当天数据时弹出确认对话框。", new FontIcon { Glyph = "\uE73E" }, _editConfirmNonTodaySaveToggle),
                    SettingsUIHelper.CreateSettingRow("编辑前刷新", "每次进入编辑模式前先从云端拉取最新数据。", new FontIcon { Glyph = "\uE72C" }, _editRefreshBeforeEditToggle)),
                SettingsUIHelper.CreateSettingsGroup("提示文案",
                    SettingsUIHelper.CreateCompoundSettingRow("自动保存提示", "自动保存模式下的底部提示文本。", _editAutoSavePromptTextBox, new FontIcon { Glyph = "\uE8A5" }),
                    SettingsUIHelper.CreateCompoundSettingRow("手动保存提示", "手动保存模式下的底部提示文本。", _editManualSavePromptTextBox, new FontIcon { Glyph = "\uE8A5" })));
        }

        protected override void LoadSettings()
        {
            var s = AppSettings.Values;
            _editAutoSaveToggle.IsOn = s.ContainsKey(EditPreferencesKeys.AutoSave) && (bool)(s[EditPreferencesKeys.AutoSave] ?? false);
            _editBlockNonTodayAutoSaveToggle.IsOn = s.ContainsKey(EditPreferencesKeys.BlockNonTodayAutoSave) && (bool)(s[EditPreferencesKeys.BlockNonTodayAutoSave] ?? false);
            _editConfirmNonTodaySaveToggle.IsOn = s.ContainsKey(EditPreferencesKeys.ConfirmNonTodaySave) && (bool)(s[EditPreferencesKeys.ConfirmNonTodaySave] ?? false);
            _editRefreshBeforeEditToggle.IsOn = s.ContainsKey(EditPreferencesKeys.RefreshBeforeEdit) && (bool)(s[EditPreferencesKeys.RefreshBeforeEdit] ?? false);
            _editAutoSavePromptTextBox.Text = s[EditPreferencesKeys.AutoSavePromptText] as string ?? "喵？喵呜！";
            _editManualSavePromptTextBox.Text = s[EditPreferencesKeys.ManualSavePromptText] as string ?? "写完后点击上传谢谢喵";
        }

        protected override void HookAutoSaveHandlers()
        {
            _editAutoSaveToggle.Toggled += (_, _) => { NotifySettingsChanged(); ScheduleEditPrefsCloudPush(); };
            _editBlockNonTodayAutoSaveToggle.Toggled += (_, _) => { NotifySettingsChanged(); ScheduleEditPrefsCloudPush(); };
            _editConfirmNonTodaySaveToggle.Toggled += (_, _) => { NotifySettingsChanged(); ScheduleEditPrefsCloudPush(); };
            _editRefreshBeforeEditToggle.Toggled += (_, _) => { NotifySettingsChanged(); ScheduleEditPrefsCloudPush(); };
            _editAutoSavePromptTextBox.TextChanged += (_, _) => { NotifySettingsChanged(); ScheduleEditPrefsCloudPush(); };
            _editManualSavePromptTextBox.TextChanged += (_, _) => { NotifySettingsChanged(); ScheduleEditPrefsCloudPush(); };
        }

        public override void PersistSettings()
        {
            var s = AppSettings.Values;
            s[EditPreferencesKeys.AutoSave] = _editAutoSaveToggle.IsOn;
            s[EditPreferencesKeys.BlockNonTodayAutoSave] = _editBlockNonTodayAutoSaveToggle.IsOn;
            s[EditPreferencesKeys.ConfirmNonTodaySave] = _editConfirmNonTodaySaveToggle.IsOn;
            s[EditPreferencesKeys.RefreshBeforeEdit] = _editRefreshBeforeEditToggle.IsOn;
            s[EditPreferencesKeys.AutoSavePromptText] = _editAutoSavePromptTextBox.Text ?? "";
            s[EditPreferencesKeys.ManualSavePromptText] = _editManualSavePromptTextBox.Text ?? "";
        }

        public override void OnNavigatedTo()
        {
            RequestEditPrefsPullFromCloudIfNeeded();
        }

        private void RequestEditPrefsPullFromCloudIfNeeded()
        {
            if (_editPrefsPulledThisSession)
                return;
            _editPrefsPulledThisSession = true;
            _ = PullEditPrefsFromCloudAndApplyUiAsync();
        }

        private async Task PullEditPrefsFromCloudAndApplyUiAsync()
        {
            var uiDq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var settings = AppSettings.Values;
            var token = settings["Token"] as string;
            if (string.IsNullOrWhiteSpace(token))
                return;

            var baseUrl = (settings["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev").TrimEnd('/');
            var ok = await EditPreferencesSync.TryPullMergeIntoAppSettingsAsync(Context.HttpClient, baseUrl, token).ConfigureAwait(false);
            if (!ok)
                return;

            uiDq?.TryEnqueue(() =>
            {
                IsAutoSaveSuspended = true;
                try
                {
                    LoadSettings();
                }
                finally
                {
                    IsAutoSaveSuspended = false;
                }

                NotifySettingsChanged();
            });
        }

        private async void ScheduleEditPrefsCloudPush()
        {
            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var generation = ++_editCloudPushGeneration;
            await Task.Delay(450).ConfigureAwait(false);
            if (generation != _editCloudPushGeneration)
                return;
            _ = await TryPushEditPrefsToKvCoreAsync(showErrors: false).ConfigureAwait(false);
            dq?.TryEnqueue(() => NotifySettingsChanged());
        }

        private async Task<bool> TryPushEditPrefsToKvCoreAsync(bool showErrors)
        {
            var settings = AppSettings.Values;
            var token = settings["Token"] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                if (showErrors)
                    await ShowSimpleDialogAsync("请先填写 KV 令牌后再同步编辑偏好。");
                return false;
            }

            var baseUrl = (settings["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev").TrimEnd('/');
            var ok = await EditPreferencesSync.PushAsync(Context.HttpClient, baseUrl, token).ConfigureAwait(false);
            if (!ok && showErrors)
                await ShowSimpleDialogAsync("编辑偏好未能写入云端，请检查网络与令牌。");
            return ok;
        }

        private async Task ShowSimpleDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "编辑偏好",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = Context.Window.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}