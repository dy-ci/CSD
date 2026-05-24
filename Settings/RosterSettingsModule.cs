using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class RosterSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "roster";
        public override string Title => "名单";
        public override string Description => "管理学生名单，支持排序、高级编辑及云端同步。";
        public override string Glyph => "\uE716";

        private TextBox _rosterNameInput = null!;
        private Grid _rosterCardsGrid = null!;
        private readonly List<string> _rosterStudents = new();
        private int _rosterCloudPushGeneration;

        protected override FrameworkElement BuildContent()
        {
            var root = new StackPanel { Spacing = 20 };

            var sortBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE8CB", "排序"), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            sortBtn.Click += (_, _) => SortRosterByName();
            
            var advBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE943", "编辑"), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            advBtn.Click += async (_, _) => await AdvancedEditRosterAsync();
            
            var cloudReloadBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE72C", "同步"), Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            cloudReloadBtn.Click += async (_, _) => await ReloadRosterFromKvAsync(showErrors: true);

            var saveRosterBtn = new Button { Content = SettingsUIHelper.CreateIconTextRow("\uE74E", "推送"), Style = (Style)Application.Current.Resources["AccentButtonStyle"], Padding = new Thickness(12, 6, 12, 6), CornerRadius = new CornerRadius(8) };
            saveRosterBtn.Click += async (_, _) => await SaveRosterToKvAsync(showErrors: true);

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            toolbar.Children.Add(sortBtn);
            toolbar.Children.Add(advBtn);
            toolbar.Children.Add(cloudReloadBtn);
            toolbar.Children.Add(saveRosterBtn);

            root.Children.Add(SettingsUIHelper.CreateSettingsGroup("操作",
                SettingsUIHelper.CreateSettingRow("名单管理", "管理学生名单并与云端同步。", new FontIcon { Glyph = "\uE716" }, toolbar)));

            _rosterNameInput = new TextBox { PlaceholderText = "输入学生姓名后按回车添加...", HorizontalAlignment = HorizontalAlignment.Stretch };
            TouchKeyboardHelper.EnableForControl(_rosterNameInput);
            _rosterNameInput.KeyDown += RosterNameInput_KeyDown;

            _rosterCardsGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            var listContainer = new StackPanel { Spacing = 12 };
            listContainer.Children.Add(_rosterNameInput);
            listContainer.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] });
            listContainer.Children.Add(new ScrollViewer { MaxHeight = 400, Content = _rosterCardsGrid });

            root.Children.Add(SettingsUIHelper.CreateSettingsGroup("名单列表",
                new Border { Padding = new Thickness(16, 12, 16, 12), Child = listContainer }));

            return root;
        }

        protected override void LoadSettings()
        {
            _rosterStudents.Clear();
            var raw = AppSettings.Values["Settings_RosterList"] as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(raw);
                    if (list != null)
                    {
                        foreach (var s in list)
                        {
                            if (!string.IsNullOrWhiteSpace(s))
                                _rosterStudents.Add(s.Trim());
                        }
                    }
                }
                catch { }
            }
            RebuildRosterGridUi();
        }

        private void RosterNameInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                TryAddRosterStudentFromInput();
                e.Handled = true;
            }
        }

        private void TryAddRosterStudentFromInput()
        {
            var name = _rosterNameInput.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) return;
            _rosterStudents.Add(name);
            _rosterNameInput.Text = "";
            RebuildRosterGridUi();
            PersistRosterLocalOnly();
        }

        private void SortRosterByName()
        {
            if (_rosterStudents.Count == 0) return;
            var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), CompareOptions.IgnoreCase);
            _rosterStudents.Sort(comparer);
            RebuildRosterGridUi();
            PersistRosterLocalOnly();
        }

        private async Task AdvancedEditRosterAsync()
        {
            var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 220, MinWidth = 360, FontFamily = new FontFamily("Consolas"), Text = string.Join(Environment.NewLine, _rosterStudents) };
            TouchKeyboardHelper.EnableForControl(box);
            var dialog = new ContentDialog { Title = "高级编辑", Content = box, PrimaryButtonText = "应用", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary, XamlRoot = Context.Window.Content.XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var lines = (box.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            _rosterStudents.Clear();
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (!string.IsNullOrEmpty(t)) _rosterStudents.Add(t);
            }
            RebuildRosterGridUi();
            PersistRosterLocalOnly();
        }

        private async Task ResetRosterWithConfirmAsync()
        {
            var dialog = new ContentDialog { Title = "重置名单", Content = "将清空当前名单（仍可随后从云端加载）。确定继续？", PrimaryButtonText = "清空", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close, XamlRoot = Context.Window.Content.XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            _rosterStudents.Clear();
            RebuildRosterGridUi();
            PersistRosterLocalOnly(queueCloudPush: false);
            await SaveRosterToKvAsync(showErrors: true);
        }

        private void PersistRosterLocalOnly(bool queueCloudPush = true)
        {
            AppSettings.Values["Settings_RosterList"] = JsonSerializer.Serialize(_rosterStudents);
            if (queueCloudPush) ScheduleRosterCloudPush();
        }

        private async void ScheduleRosterCloudPush()
        {
            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var generation = ++_rosterCloudPushGeneration;
            await Task.Delay(450).ConfigureAwait(false);
            if (generation != _rosterCloudPushGeneration) return;
            var ok = await TryPushRosterToKvCoreAsync(showErrors: false).ConfigureAwait(false);
            if (ok) dq?.TryEnqueue(() => NotifySettingsChanged());
        }

        private async Task<bool> TryPushRosterToKvCoreAsync(bool showErrors)
        {
            var payload = new List<Dictionary<string, object>>();
            for (var i = 0; i < _rosterStudents.Count; i++)
                payload.Add(new Dictionary<string, object> { ["order"] = i + 1, ["name"] = _rosterStudents[i] });
            var json = JsonSerializer.Serialize(payload);

            var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
            if (dataProvider == "本地存储")
            {
                var localResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Post, $"/kv/{ClassworksKvKeys.RosterConfig}", json);
                return localResponse.IsSuccessStatusCode;
            }

            var token = AppSettings.Values["Token"] as string;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var baseUrl = (AppSettings.Values["Settings_ServerUrl"] as string ?? "https://kv-service.wuyuan.dev").TrimEnd('/');
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/kv/{ClassworksKvKeys.RosterConfig}") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
                using var response = await Context.HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    if (showErrors) await ShowSimpleDialogAsync($"名单同步失败（HTTP {(int)response.StatusCode}）。");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors) await ShowSimpleDialogAsync($"名单同步失败：{ex.Message}");
                return false;
            }
        }

        private async Task ReloadRosterFromKvAsync(bool showErrors)
        {
            string body;
            var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
            if (dataProvider == "本地存储")
            {
                var localResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Get, $"/kv/{ClassworksKvKeys.RosterConfig}", null);
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
                    if (showErrors) await ShowSimpleDialogAsync("请先配置 KV 授权令牌后再从云端加载名单。");
                    LoadSettings();
                    return;
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/kv/{ClassworksKvKeys.RosterConfig}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
                    using var response = await Context.HttpClient.SendAsync(request);
                    body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        if (showErrors) await ShowSimpleDialogAsync($"从云端加载名单失败（HTTP {(int)response.StatusCode}）。已显示本机缓存。");
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
                var names = ParseRosterNamesFromJson(doc.RootElement);
                _rosterStudents.Clear();
                _rosterStudents.AddRange(names);
                RebuildRosterGridUi();
                PersistRosterLocalOnly(queueCloudPush: false);
                NotifySettingsChanged();
            }
            catch (Exception ex)
            {
                if (showErrors) await ShowSimpleDialogAsync($"加载名单失败：{ex.Message}");
                LoadSettings();
            }
        }

        private static List<string> ParseRosterNamesFromJson(JsonElement root)
        {
            var pairs = new List<(int order, string name)>();
            if (root.ValueKind != JsonValueKind.Array) return new List<string>();
            foreach (var el in root.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var n = el.GetString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(n)) pairs.Add((pairs.Count, n));
                    continue;
                }
                if (el.ValueKind != JsonValueKind.Object) continue;
                var order = pairs.Count;
                if (el.TryGetProperty("order", out var oEl) && oEl.ValueKind == JsonValueKind.Number) order = oEl.GetInt32();
                var name = "";
                if (el.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String) name = nEl.GetString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(name)) pairs.Add((order, name));
            }
            pairs.Sort((a, b) => a.order.CompareTo(b.order));
            return pairs.ConvertAll(p => p.name);
        }

        private async Task SaveRosterToKvAsync(bool showErrors)
        {
            var token = AppSettings.Values["Token"] as string;
            PersistRosterLocalOnly(queueCloudPush: false);
            if (string.IsNullOrWhiteSpace(token))
            {
                if (showErrors) await ShowSimpleDialogAsync("名单已保存到本机。填写 KV 令牌后可同步到云端。");
                NotifySettingsChanged();
                return;
            }
            var ok = await TryPushRosterToKvCoreAsync(showErrors);
            if (ok) NotifySettingsChanged();
        }

        private void RebuildRosterGridUi()
        {
            _rosterCardsGrid.Children.Clear();
            _rosterCardsGrid.RowDefinitions.Clear();
            _rosterCardsGrid.ColumnDefinitions.Clear();

            const int cols = 4;
            var count = _rosterStudents.Count;
            var rows = count == 0 ? 1 : (count + cols - 1) / cols;
            for (var c = 0; c < cols; c++) _rosterCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < rows; r++) _rosterCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var i = 0; i < count; i++)
            {
                var row = i / cols;
                var col = i % cols;
                var card = CreateStudentCard(i + 1, _rosterStudents[i], i);
                Grid.SetRow(card, row);
                Grid.SetColumn(card, col);
                _rosterCardsGrid.Children.Add(card);
            }
        }

        private Border CreateStudentCard(int displayNumber, string name, int listIndex)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
            var editBtn = new Button { Content = new FontIcon { Glyph = "\uE70F", FontSize = 14 }, Padding = new Thickness(4), MinWidth = 32, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
            var capturedIdx = listIndex;
            editBtn.Click += async (_, _) => await EditRosterStudentAsync(capturedIdx);
            var delBtn = new Button { Content = new FontIcon { Glyph = "\uE74D", FontSize = 14 }, Padding = new Thickness(4), MinWidth = 32, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] };
            delBtn.Click += (_, _) => RemoveRosterAt(capturedIdx);
            actions.Children.Add(editBtn); actions.Children.Add(delBtn);

            var inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var numBorder = new Border { MinWidth = 28, Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(4), Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"], Child = new TextBlock { Text = displayNumber.ToString(CultureInfo.InvariantCulture), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] } };
            Grid.SetColumn(numBorder, 0);

            var nameTb = new TextBlock { Text = name, FontSize = 15, Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(nameTb, 1);

            Grid.SetColumn(actions, 2);
            inner.Children.Add(numBorder); inner.Children.Add(nameTb); inner.Children.Add(actions);

            var card = new Border { Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"], CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 8, 8), Margin = new Thickness(4), Child = inner };
            card.PointerEntered += (_, _) => { actions.Opacity = 1; };
            card.PointerExited += (_, _) => { actions.Opacity = 0; };
            return card;
        }

        private async Task EditRosterStudentAsync(int index)
        {
            if (index < 0 || index >= _rosterStudents.Count) return;
            var box = new TextBox { Text = _rosterStudents[index], Width = 280 };
            TouchKeyboardHelper.EnableForControl(box);
            var dialog = new ContentDialog { Title = "编辑姓名", Content = box, PrimaryButtonText = "确定", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary, XamlRoot = Context.Window.Content.XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var t = box.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(t)) return;
            _rosterStudents[index] = t;
            RebuildRosterGridUi();
            PersistRosterLocalOnly();
        }

        private void RemoveRosterAt(int index)
        {
            if (index < 0 || index >= _rosterStudents.Count) return;
            _rosterStudents.RemoveAt(index);
            RebuildRosterGridUi();
            PersistRosterLocalOnly();
        }

        private async Task ShowSimpleDialogAsync(string message)
        {
            var dialog = new ContentDialog { Title = "名单管理", Content = message, CloseButtonText = "确定", XamlRoot = Context.Window.Content.XamlRoot };
            await dialog.ShowAsync();
        }
    }
}