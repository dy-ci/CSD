using Microsoft.UI;
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
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Views
{
    public enum AttendanceStatus
    {
        Present,
        Leave,
        Late,
        Absent
    }

    public class StudentAttendance
    {
        public string Name { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    }

    public sealed partial class AttendanceWindow : Window
    {
        private readonly DateTime _attendanceDate;
        private readonly List<StudentAttendance> _students = new();
        private readonly List<StudentAttendance> _filteredStudents = new();
        private readonly Dictionary<StudentAttendance, Border> _studentCards = new();
        private CancellationTokenSource? _searchCts;

        private static readonly FontFamily MdiFont = new(AppSettings.GetAssetUri("Assets/MaterialDesignIconsDesktop.ttf").AbsoluteUri + "#Material Design Icons");

        public AttendanceWindow(DateTime attendanceDate)
        {
            InitializeComponent();
            _attendanceDate = attendanceDate;
            TouchKeyboardHelper.EnableForControl(SearchBox);
            VisualHelper.ApplyWindowBackdrop(this);

            ExtendsContentIntoTitleBar = true;
            this.AppWindow.Resize(new SizeInt32(900, 600));

            try
            {
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch { }

            _ = LoadAttendanceDataAsync();
        }

        private string BaseUrl
        {
            get
            {
                var url = AppSettings.Values["Settings_ServerUrl"] as string;
                return string.IsNullOrWhiteSpace(url) ? "https://kv-service.wuyuan.dev" : url;
            }
        }

        private async Task LoadAttendanceDataAsync()
        {
            var token = AppSettings.Values["Token"] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var dateKey = $"classworks-data-{_attendanceDate:yyyyMMdd}";
            var responseBody = await SendKvRequestAsync(HttpMethod.Get, $"/kv/{Uri.EscapeDataString(dateKey)}");

            var statusMap = new Dictionary<string, AttendanceStatus>();

            if (responseBody != null)
            {
                try
                {
                    using var document = JsonDocument.Parse(responseBody);
                    if (document.RootElement.TryGetProperty("attendance", out var attendanceElement) && attendanceElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var name in ParseStringArray(attendanceElement, "absent")) statusMap[name] = AttendanceStatus.Leave;
                        foreach (var name in ParseStringArray(attendanceElement, "late")) statusMap[name] = AttendanceStatus.Late;
                        foreach (var name in ParseStringArray(attendanceElement, "exclude")) statusMap[name] = AttendanceStatus.Absent;
                    }
                }
                catch
                {
                }
            }

            var studentList = await LoadStudentListAsync();
            foreach (var name in studentList)
            {
                _students.Add(new StudentAttendance
                {
                    Name = name,
                    Status = statusMap.TryGetValue(name, out var status) ? status : AttendanceStatus.Present
                });
            }

            _filteredStudents.Clear();
            _filteredStudents.AddRange(_students);

            BuildStudentsGrid();
            UpdateStats();
        }

        private List<string> ParseStringArray(JsonElement parent, string propertyName)
        {
            var result = new List<string>();
            if (parent.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result.Add(name);
                    }
                }
            }
            return result;
        }

        private async Task<List<string>> LoadStudentListAsync()
        {
            var listResponse = await SendKvRequestAsync(HttpMethod.Get, "/kv/classworks-list-main");
            var students = new List<string>();

            if (string.IsNullOrWhiteSpace(listResponse))
            {
                return students;
            }

            try
            {
                using var document = JsonDocument.Parse(listResponse);
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            students.Add(name);
                    }
                }
            }
            catch
            {
            }

            return students;
        }

        private async Task<string?> SendKvRequestAsync(HttpMethod method, string path, string? jsonBody = null)
        {
            var dataProvider = AppSettings.Values["Settings_DataProvider"] as string;
            if (dataProvider == "本地存储")
            {
                var localResponse = await LocalKvStorageEngine.HandleRequestAsync(method, path, jsonBody);
                if (localResponse.IsSuccessStatusCode)
                {
                    return await localResponse.Content.ReadAsStringAsync();
                }
                return null;
            }

            var token = AppSettings.Values["Token"] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                using var request = new HttpRequestMessage(method, BaseUrl + path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                if (jsonBody is not null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                                using var response = await AppHttpClient.Instance.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return responseBody;
            }
            catch
            {
                return null;
            }
        }

        private void BuildStudentsGrid()
        {
            StudentsPanel.Children.Clear();
            _studentCards.Clear();

            var headerPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 12) };

            var headerIcon = new TextBlock
            {
                Text = char.ConvertFromUtf32(0xF0849),
                FontFamily = MdiFont,
                FontSize = 32,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            headerPanel.Children.Add(headerIcon);

            var headerText = new TextBlock
            {
                Text = $"考勤 — {_attendanceDate:yyyy年M月d日}",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            };
            headerPanel.Children.Add(headerText);

            StudentsPanel.Children.Add(headerPanel);

            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8
            };

            int itemsPerRow = 4;
            int rows = (_filteredStudents.Count + itemsPerRow - 1) / itemsPerRow;

            for (int r = 0; r < rows; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int c = 0; c < itemsPerRow; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int i = 0; i < _filteredStudents.Count; i++)
            {
                var student = _filteredStudents[i];
                int row = i / itemsPerRow;
                int col = i % itemsPerRow;

                var card = BuildStudentCard(student);
                Grid.SetRow(card, row);
                Grid.SetColumn(card, col);
                grid.Children.Add(card);
            }

            StudentsPanel.Children.Add(grid);
        }

        private Border BuildStudentCard(StudentAttendance student)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                Child = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                }
            };

            var nameText = new TextBlock
            {
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Text = student.Name
            };
            Grid.SetColumn(nameText, 0);

            var statusButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnPresent = CreateStatusButton(char.ConvertFromUtf32(0xF0008), AttendanceStatus.Present, new Color { R = 76, G = 175, B = 80, A = 255 }, student);
            var btnLeave = CreateStatusButton(char.ConvertFromUtf32(0xF0012), AttendanceStatus.Leave, new Color { R = 244, G = 67, B = 54, A = 255 }, student);
            var btnLate = CreateStatusButton(char.ConvertFromUtf32(0xF05CE), AttendanceStatus.Late, new Color { R = 255, G = 193, B = 7, A = 255 }, student);
            var btnAbsent = CreateStatusButton(char.ConvertFromUtf32(0xF12DF), AttendanceStatus.Absent, new Color { R = 158, G = 158, B = 158, A = 255 }, student);

            statusButtons.Children.Add(btnPresent);
            statusButtons.Children.Add(btnLeave);
            statusButtons.Children.Add(btnLate);
            statusButtons.Children.Add(btnAbsent);

            Grid.SetColumn(statusButtons, 1);

            ((Grid)card.Child).Children.Add(nameText);
            ((Grid)card.Child).Children.Add(statusButtons);

            _studentCards[student] = card;
            UpdateCardButtons(card, student);

            return card;
        }

        private Button CreateStatusButton(string icon, AttendanceStatus status, Color color, StudentAttendance student)
        {
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = icon,
                    FontFamily = MdiFont,
                    FontSize = 22,
                    Foreground = new SolidColorBrush(color)
                },
                MinWidth = 36,
                MinHeight = 36,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1),
                Tag = status,
                DataContext = student
            };

            btn.Click += (sender, e) =>
            {
                var b = sender as Button;
                if (b?.DataContext is not StudentAttendance stu)
                    return;
                if (b?.Tag is not AttendanceStatus st)
                    return;

                stu.Status = st;

                if (_studentCards.TryGetValue(stu, out var card))
                {
                    UpdateCardButtons(card, stu);
                }

                UpdateStats();
            };

            return btn;
        }

        private void UpdateCardButtons(Border card, StudentAttendance student)
        {
            if (card.Child is not Grid grid) return;

            foreach (var child in grid.Children)
            {
                if (child is StackPanel panel)
                {
                    foreach (var item in panel.Children)
                    {
                        if (item is Button btn && btn.Tag is AttendanceStatus btnStatus)
                        {
                            Color c = btnStatus switch
                            {
                                AttendanceStatus.Present => new Color { R = 76, G = 175, B = 80, A = 255 },
                                AttendanceStatus.Leave => new Color { R = 244, G = 67, B = 54, A = 255 },
                                AttendanceStatus.Late => new Color { R = 255, G = 193, B = 7, A = 255 },
                                AttendanceStatus.Absent => new Color { R = 158, G = 158, B = 158, A = 255 },
                                _ => new Color { R = 158, G = 158, B = 158, A = 255 }
                            };

                            if (btnStatus == student.Status)
                            {
                                btn.Background = new SolidColorBrush(c);
                                btn.BorderBrush = new SolidColorBrush(c);
                                if (btn.Content is TextBlock tb)
                                    tb.Foreground = new SolidColorBrush(Colors.White);
                            }
                            else
                            {
                                btn.Background = new SolidColorBrush(Colors.Transparent);
                                btn.BorderBrush = new SolidColorBrush(Colors.Transparent);
                                if (btn.Content is TextBlock tb)
                                    tb.Foreground = new SolidColorBrush(c);
                            }
                        }
                    }
                }
            }
        }

        public void UpdateStats()
        {
            int present = 0, leave = 0, late = 0, absent = 0;
            foreach (var s in _students)
            {
                switch (s.Status)
                {
                    case AttendanceStatus.Present: present++; break;
                    case AttendanceStatus.Leave:   leave++;   break;
                    case AttendanceStatus.Late:    late++;    break;
                    case AttendanceStatus.Absent:  absent++;  break;
                }
            }
            int total = _students.Count;
            StatsText.Text = $"到课 {present} | 请假 {leave} | 迟到 {late} | 不参与 {absent} | 共 {total}";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            _ = Task.Delay(200, token).ContinueWith(_ =>
            {
                if (token.IsCancellationRequested) return;
                var searchText = SearchBox.Text.Trim().ToLower();

                _filteredStudents.Clear();
                foreach (var student in _students)
                {
                    if (string.IsNullOrEmpty(searchText) || student.Name.ToLower().Contains(searchText))
                    {
                        _filteredStudents.Add(student);
                    }
                }

                DispatcherQueue.TryEnqueue(() => BuildStudentsGrid());
            }, token);
        }

        private void SetAllStatus(AttendanceStatus status)
        {
            foreach (var student in _students)
            {
                student.Status = status;
            }

            foreach (var kvp in _studentCards)
            {
                UpdateCardButtons(kvp.Value, kvp.Key);
            }

            UpdateStats();
        }

        private void BtnAllPresent_Click(object sender, RoutedEventArgs e) => SetAllStatus(AttendanceStatus.Present);
        private void BtnAllLeave_Click(object sender, RoutedEventArgs e) => SetAllStatus(AttendanceStatus.Leave);
        private void BtnAllLate_Click(object sender, RoutedEventArgs e) => SetAllStatus(AttendanceStatus.Late);
        private void BtnAllAbsent_Click(object sender, RoutedEventArgs e) => SetAllStatus(AttendanceStatus.Absent);

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var token = AppSettings.Values["Token"] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var dateKey = $"classworks-data-{_attendanceDate:yyyyMMdd}";
            var responseBody = await SendKvRequestAsync(HttpMethod.Get, $"/kv/{Uri.EscapeDataString(dateKey)}");

            var homeworkDict = new Dictionary<string, object>();
            if (responseBody != null)
            {
                try
                {
                    using var document = JsonDocument.Parse(responseBody);
                    if (document.RootElement.TryGetProperty("homework", out var homeworkElement) && homeworkElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var subj in homeworkElement.EnumerateObject())
                        {
                            if (subj.Value.ValueKind == JsonValueKind.Object)
                            {
                                var inner = new Dictionary<string, object>();
                                foreach (var p in subj.Value.EnumerateObject())
                                {
                                    inner[p.Name] = p.Value.ValueKind == JsonValueKind.String
                                        ? p.Value.GetString()!
                                        : p.Value.GetRawText();
                                }
                                homeworkDict[subj.Name] = inner;
                            }
                            else
                            {
                                homeworkDict[subj.Name] = subj.Value.GetRawText();
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            var leaveList = _students.Where(s => s.Status == AttendanceStatus.Leave).Select(s => s.Name).ToList();
            var lateList = _students.Where(s => s.Status == AttendanceStatus.Late).Select(s => s.Name).ToList();
            var excludeList = _students.Where(s => s.Status == AttendanceStatus.Absent).Select(s => s.Name).ToList();

            var attendanceDict = new Dictionary<string, object>();
            if (leaveList.Count > 0) attendanceDict["absent"] = leaveList;
            if (lateList.Count > 0) attendanceDict["late"] = lateList;
            if (excludeList.Count > 0) attendanceDict["exclude"] = excludeList;

            var payload = new Dictionary<string, object>
            {
                ["homework"] = homeworkDict,
            };
            if (attendanceDict.Count > 0)
                payload["attendance"] = attendanceDict;

            var json = JsonSerializer.Serialize(payload);
            var response = await SendKvRequestAsync(HttpMethod.Post, $"/kv/{Uri.EscapeDataString(dateKey)}", json);

            if (response != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "保存成功",
                    Content = new TextBlock { Text = $"考勤数据已保存（{_attendanceDate:yyyy-MM-dd}）" },
                    CloseButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }
}
