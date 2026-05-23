using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
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
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed;

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
            await _connectionLock.WaitAsync();
            try
            {
                if (_disposed) return;
                if (_client != null && _connectedDomain == serverUrl)
                {
                    return;
                }

                await DisconnectInternalAsync();

                _connectedDomain = serverUrl;
                var client = new SocketIO(new Uri(serverUrl), new SocketIOOptions
                {
                    EIO = SocketIOClient.Common.EngineIO.V4,
                    Reconnection = true
                });

                client.OnConnected += async (sender, e) =>
                {
                    Debug.WriteLine("Socket.IO Connected to " + serverUrl);
                    var token = GetKvToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        await JoinTokenAsync(token);
                    }
                };

                client.OnDisconnected += (sender, e) =>
                {
                    Debug.WriteLine("Socket.IO Disconnected");
                };

                // Register Event Handlers based on Classworks-main logic
                client.On("chat", async context =>
                {
                    var data = context.GetValue<JsonElement>(0);
                    Debug.WriteLine("Socket.IO Received chat: " + data.GetRawText());
                    OnChatReceived?.Invoke(data.GetRawText());
                    await Task.CompletedTask;
                });
                
                client.On("chat:message", async context =>
                {
                    var data = context.GetValue<JsonElement>(0);
                    Debug.WriteLine("Socket.IO Received chat:message: " + data.GetRawText());
                    OnChatReceived?.Invoke(data.GetRawText());
                    await Task.CompletedTask;
                });

                client.On("kv-key-changed", async context =>
                {
                    var data = context.GetValue<JsonElement>(0);
                    Debug.WriteLine("Socket.IO Received kv-key-changed: " + data.GetRawText());
                    OnKvKeyChanged?.Invoke(data.GetRawText());
                    await Task.CompletedTask;
                });

                client.On("urgent-notice", async context =>
                {
                    var data = context.GetValue<JsonElement>(0);
                    Debug.WriteLine("Socket.IO Received urgent-notice: " + data.GetRawText());
                    OnUrgentNotice?.Invoke(data.GetRawText());
                    await Task.CompletedTask;
                });

                client.On("notification", async context =>
                {
                    var data = context.GetValue<JsonElement>(0);
                    Debug.WriteLine("Socket.IO Received notification: " + data.GetRawText());
                    OnNotification?.Invoke(data.GetRawText());
                    await Task.CompletedTask;
                });

                _client = client;

                try
                {
                    await _client.ConnectAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Socket.IO Connection Error: " + ex.Message);
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task JoinTokenAsync(string token)
        {
            var client = _client;
            if (client != null && client.Connected)
            {
                await client.EmitAsync("join-token", new object[] { new { token } });
                Debug.WriteLine($"Socket.IO Joined token: {token}");
            }
        }

        public async Task LeaveTokenAsync(string token)
        {
            var client = _client;
            if (client != null && client.Connected)
            {
                await client.EmitAsync("leave-token", new object[] { new { token } });
            }
        }

        public async Task SendEventAsync(string type, object content)
        {
            var client = _client;
            if (client != null && client.Connected)
            {
                await client.EmitAsync("send-event", new object[] { new { type, content } });
            }
        }

        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                await DisconnectInternalAsync();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task DisconnectInternalAsync()
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

        public async Task DisposeAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                _disposed = true;
                await DisconnectInternalAsync();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public void Dispose()
        {
            _ = DisposeAsync();
        }
    }
}
