using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using SocketIOClient;
using CSD.Models;

namespace CSD.Services
{
    public class SocketIoService : IDisposable
    {
        private static SocketIoService? _instance;
        public static SocketIoService Instance => _instance ??= new SocketIoService();

        private SocketIO? _client;
        private string _connectedDomain = string.Empty;

        public event Action<string>? OnChatReceived;
        public event Action<string>? OnKvKeyChanged;
        public event Action<string>? OnUrgentNotice;
        public event Action<string>? OnNotification;

        private SocketIoService()
        {
        }

        public string GetServerUrl()
        {
            var cfg = AppSettings.Values.TryGetValue("Settings_ServerUrl", out var val) ? val?.ToString() : null;
            return string.IsNullOrWhiteSpace(cfg) ? "https://kv-service.wuyuan.dev" : cfg;
        }

        public string? GetKvToken()
        {
            return AppSettings.Values.TryGetValue("Token", out var val) ? val?.ToString() : null;
        }

        public async Task ConnectAsync()
        {
            var serverUrl = GetServerUrl();
            if (_client != null && _connectedDomain == serverUrl)
            {
                return;
            }

            await DisconnectAsync();

            _connectedDomain = serverUrl;
            _client = new SocketIO(new Uri(serverUrl), new SocketIOOptions
            {
                EIO = SocketIOClient.Common.EngineIO.V4,
                Reconnection = true
            });

            _client.OnConnected += async (sender, e) =>
            {
                Debug.WriteLine("Socket.IO Connected to " + serverUrl);
                var token = GetKvToken();
                if (!string.IsNullOrEmpty(token))
                {
                    await JoinTokenAsync(token);
                }
            };

            _client.OnDisconnected += (sender, e) =>
            {
                Debug.WriteLine("Socket.IO Disconnected");
            };

            // Register Event Handlers based on Classworks-main logic
            _client.On("chat", async context =>
            {
                var data = context.GetValue<JsonElement>(0);
                Debug.WriteLine("Socket.IO Received chat: " + data.GetRawText());
                OnChatReceived?.Invoke(data.GetRawText());
                await Task.CompletedTask;
            });
            
            _client.On("chat:message", async context =>
            {
                var data = context.GetValue<JsonElement>(0);
                Debug.WriteLine("Socket.IO Received chat:message: " + data.GetRawText());
                OnChatReceived?.Invoke(data.GetRawText());
                await Task.CompletedTask;
            });

            _client.On("kv-key-changed", async context =>
            {
                var data = context.GetValue<JsonElement>(0);
                Debug.WriteLine("Socket.IO Received kv-key-changed: " + data.GetRawText());
                OnKvKeyChanged?.Invoke(data.GetRawText());
                await Task.CompletedTask;
            });

            _client.On("urgent-notice", async context =>
            {
                var data = context.GetValue<JsonElement>(0);
                Debug.WriteLine("Socket.IO Received urgent-notice: " + data.GetRawText());
                OnUrgentNotice?.Invoke(data.GetRawText());
                await Task.CompletedTask;
            });

            _client.On("notification", async context =>
            {
                var data = context.GetValue<JsonElement>(0);
                Debug.WriteLine("Socket.IO Received notification: " + data.GetRawText());
                OnNotification?.Invoke(data.GetRawText());
                await Task.CompletedTask;
            });

            try
            {
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Socket.IO Connection Error: " + ex.Message);
            }
        }

        public async Task JoinTokenAsync(string token)
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("join-token", new object[] { new { token } });
                Debug.WriteLine($"Socket.IO Joined token: {token}");
            }
        }

        public async Task LeaveTokenAsync(string token)
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("leave-token", new object[] { new { token } });
            }
        }

        public async Task SendEventAsync(string type, object content)
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("send-event", new object[] { new { type, content } });
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client != null)
            {
                if (_client.Connected)
                {
                    await _client.DisconnectAsync();
                }
                _client.Dispose();
                _client = null;
            }
            _connectedDomain = string.Empty;
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
        }
    }
}
