using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class SubjectSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "subjects";
        public override string Title => "科目";
        public override string Description => "管理作业科目列表，并可将其同步到云端。";
        public override string Glyph => "\uE70F";

        private TextBox _subjectNameInput = null!;
        private StackPanel _subjectRowsPanel = null!;
        private readonly List<string> _managedSubjects = new();
        private int _subjectCloudPushGeneration;

        private static readonly string[] DefaultSubjectNames = ["语文", "数学", "英语", "物理", "化学", "生物", "政治", "历史", "地理", "其他"];

        protected override FrameworkElement BuildContent()
        {
            var root = new StackPanel { Spacing = 20 };

            var reloadBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE72C", "同步"), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            reloadBtn.Click += async (_, _) => await ReloadSubjectsFromKvAsync(showErrors: true);

            var saveBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE74E", "推送"), Style = (Style)Application.Current.Resources["AccentButtonStyle"], Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            saveBtn.Click += async (_, _) => await SaveSubjectsToKvAsync(showErrors: true);

            var resetBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE777", "重置"), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            resetBtn.Click += (_, _) => ResetSubjectsToDefaults(queueCloudSync: true);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            btnRow.Children.Add(reloadBtn);
            btnRow.Children.Add(saveBtn);
            btnRow.Children.Add(resetBtn);

            root.Children.Add(SettingsUIHelper.CreateSettingsGroup("操作",
                SettingsUIHelper.CreateSettingRow("云端同步", "从服务器拉取或推送科目配置。", new FontIcon { Glyph = "\uE753" }, btnRow)));

            _subjectNameInput = new TextBox { PlaceholderText = "输入科目名称后按回车添加...", HorizontalAlignment = HorizontalAlignment.Stretch };
            TouchKeyboardHelper.EnableForControl(_subjectNameInput);
            _subjectNameInput.KeyDown += SubjectNameInput_KeyDown;

            _subjectRowsPanel = new StackPanel();
            var listContainer = new StackPanel { Spacing = 12 };
            listContainer.Children.Add(_subjectNameInput);
            listContainer.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] });
            listContainer.Children.Add(new ScrollViewer { MaxHeight = 400, Content = _subjectRowsPanel });

            root.Children.Add(SettingsUIHelper.CreateSettingsGroup("科目列表",
                new Border { Padding = new Thickness(16, 12, 16, 12), Child = listContainer }));

            return root;
        }

        protected override void LoadSettings()
        {
            var raw = AppSettings.Values["Settings_SubjectList"] as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(raw);
                    if (list != null && list.Count > 0)
                    {
                        _managedSubjects.Clear();
                        foreach (var s in list)
                        {
                            if (!string.IsNullOrWhiteSpace(s))
                                _managedSubjects.Add(s.Trim());
                        }
                        if (_managedSubjects.Count > 0)
                        {
                            RebuildSubjectListUi();
                            return;
                        }
                    }
                }
                catch { }
            }

            if (_managedSubjects.Count == 0)
                ResetSubjectsToDefaults(queueCloudSync: false);
            else
                RebuildSubjectListUi();
        }

        private void SubjectNameInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var name = _subjectNameInput.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    _managedSubjects.Add(name);
                    _subjectNameInput.Text = "";
                    RebuildSubjectListUi();
                    PersistSubjectListLocalOnly();
                }
                e.Handled = true;
            }
        }

        private void ResetSubjectsToDefaults(bool queueCloudSync = true)
        {
            _managedSubjects.Clear();
            _managedSubjects.AddRange(DefaultSubjectNames);
            RebuildSubjectListUi();
            PersistSubjectListLocalOnly(queueCloudPush: queueCloudSync);
        }

        private void PersistSubjectListLocalOnly(bool queueCloudPush = true)
        {
            AppSettings.Values["Settings_SubjectList"] = JsonSerializer.Serialize(_managedSubjects);
            if (queueCloudPush) ScheduleSubjectsCloudPush();
        }

        private async void ScheduleSubjectsCloudPush()
        {
            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var generation = ++_subjectCloudPushGeneration;
            await Task.Delay(450).ConfigureAwait(false);
            if (generation != _subjectCloudPushGeneration) return;
            var ok = await TryPushSubjectsToKvCoreAsync(showErrors: false).ConfigureAwait(false);
            if (ok) dq?.TryEnqueue(() => NotifySettingsChanged());
        }

        private async Task<bool> TryPushSubjectsToKvCoreAsync(bool showErrors)
        {
            var payload = new List<Dictionary<string, object>>();
            for (var i = 0; i < _managedSubjects.Count; i++)
                payload.Add(new Dictionary<string, object> { ["order"] = i + 1, ["name"] = _managedSubjects[i] });
            var json = JsonSerializer.Serialize(payload);

            var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
            if (dataProvider == "本地存储")
            {
                var localResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Post, $"/kv/{ClassworksKvKeys.SubjectConfig}", json);
                return localResponse.IsSuccessStatusCode;
            }

            var token = AppSettings.Values["Token"] as string;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var baseUrl = (AppSettings.Values["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev").TrimEnd('/');
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/kv/{ClassworksKvKeys.SubjectConfig}") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
                using var response = await Context.HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    if (showErrors) await ShowSimpleDialogAsync($"同步到服务器失败（HTTP {(int)response.StatusCode}）。本机列表已保留。");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors) await ShowSimpleDialogAsync($"同步失败：{ex.Message}");
                return false;
            }
        }

        private void RebuildSubjectListUi()
        {
            _subjectRowsPanel.Children.Clear();
            for (int i = 0; i < _managedSubjects.Count; i++)
            {
                var idx = i;
                var row = new Grid { Padding = new Thickness(8, 8, 12, 8), MinHeight = 48 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var moveCol = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                var up = new Button { Content = new FontIcon { Glyph = "\uE70E", FontSize = 11 }, Padding = new Thickness(4, 2, 4, 2), MinWidth = 34, MinHeight = 26, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
                up.Click += (_, _) => MoveSubject(idx, -1);
                var down = new Button { Content = new FontIcon { Glyph = "\uE70D", FontSize = 11 }, Padding = new Thickness(4, 2, 4, 2), MinWidth = 34, MinHeight = 26, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
                down.Click += (_, _) => MoveSubject(idx, 1);
                moveCol.Children.Add(up); moveCol.Children.Add(down);
                Grid.SetColumn(moveCol, 0);

                var nameTb = new TextBlock { Text = _managedSubjects[i], FontSize = 15, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(nameTb, 1);

                var deleteBtn = new Button { Content = new FontIcon { Glyph = "\uE74D", FontSize = 14 }, MinWidth = 40, MinHeight = 36, Padding = new Thickness(6), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] };
                ToolTipService.SetToolTip(deleteBtn, "删除");
                deleteBtn.Click += (_, _) => { _managedSubjects.RemoveAt(idx); RebuildSubjectListUi(); PersistSubjectListLocalOnly(); };
                Grid.SetColumn(deleteBtn, 2);

                row.Children.Add(moveCol); row.Children.Add(nameTb); row.Children.Add(deleteBtn);
                _subjectRowsPanel.Children.Add(row);
            }
        }

        private void MoveSubject(int index, int delta)
        {
            var n = index + delta;
            if (n < 0 || n >= _managedSubjects.Count) return;
            (_managedSubjects[index], _managedSubjects[n]) = (_managedSubjects[n], _managedSubjects[index]);
            RebuildSubjectListUi();
            PersistSubjectListLocalOnly();
        }

        private async Task ReloadSubjectsFromKvAsync(bool showErrors)
        {
            string body;
            var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
            if (dataProvider == "本地存储")
            {
                var localResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Get, $"/kv/{ClassworksKvKeys.SubjectConfig}", null);
                if (!localResponse.IsSuccessStatusCode)
                {
                    if (showErrors) await ShowSimpleDialogAsync("从本地存储加载失败。已显示本机缓存。");
                    LoadSettings();
                    return;
                }
                body = await localResponse.Content.ReadAsStringAsync();
            }
            else
            {
                var token = AppSettings.Values["Token"] as string;
                var baseUrl = (AppSettings.Values["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev").TrimEnd('/');
                if (string.IsNullOrWhiteSpace(token))
                {
                    if (showErrors) await ShowSimpleDialogAsync("请先配置 KV 授权令牌后再从服务器加载科目列表。");
                    LoadSettings();
                    return;
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/kv/{ClassworksKvKeys.SubjectConfig}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
                    using var response = await Context.HttpClient.SendAsync(request);
                    body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        if (showErrors) await ShowSimpleDialogAsync($"从服务器加载失败（HTTP {(int)response.StatusCode}）。已显示本机已保存的列表。");
                        LoadSettings();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (showErrors) await ShowSimpleDialogAsync($"加载失败：{ex.Message}");
                    LoadSettings();
                    return;
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var pairs = new List<(int order, string name)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var order = pairs.Count;
                    if (el.TryGetProperty("order", out var oEl) && oEl.ValueKind == JsonValueKind.Number) order = oEl.GetInt32();
                    var name = "";
                    if (el.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String) name = nEl.GetString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(name)) pairs.Add((order, name));
                }

                pairs.Sort((a, b) => a.order.CompareTo(b.order));
                _managedSubjects.Clear();
                _managedSubjects.AddRange(pairs.Select(p => p.name));
                if (_managedSubjects.Count == 0) LoadSettings();
                else
                {
                    RebuildSubjectListUi();
                    PersistSubjectListLocalOnly(queueCloudPush: false);
                    NotifySettingsChanged();
                }
            }
            catch (Exception ex)
            {
                if (showErrors) await ShowSimpleDialogAsync($"加载失败：{ex.Message}");
                LoadSettings();
            }
        }

        private async Task SaveSubjectsToKvAsync(bool showErrors)
        {
            var token = AppSettings.Values["Token"] as string;
            PersistSubjectListLocalOnly(queueCloudPush: false);
            if (string.IsNullOrWhiteSpace(token))
            {
                if (showErrors) await ShowSimpleDialogAsync("列表已保存到本机。若要同步到云端，请先填写 KV 授权令牌后再点保存。");
                NotifySettingsChanged();
                return;
            }

            var ok = await TryPushSubjectsToKvCoreAsync(showErrors);
            if (ok) NotifySettingsChanged();
        }

        private async Task ShowSimpleDialogAsync(string message)
        {
            var dialog = new ContentDialog { Title = "科目管理", Content = message, CloseButtonText = "确定", XamlRoot = Context.Window.Content.XamlRoot };
            await dialog.ShowAsync();
        }
    }
}