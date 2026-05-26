using System.Collections.Generic;
using System.Text.Json;

namespace CSD.Models
{
    public class NotificationPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? NotificationId { get; set; }
        public bool IsUrgent { get; set; }
        public List<NotificationButton> Buttons { get; set; } = [];

        public static NotificationPayload Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var payload = new NotificationPayload();

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Object)
                {
                    ParseContent(contentElement, payload);
                }
                else
                {
                    ParseFlat(doc.RootElement, payload);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                payload.Message = doc.RootElement.GetString() ?? string.Empty;
            }

            return payload;
        }

        private static void ParseContent(JsonElement element, NotificationPayload payload)
        {
            if (element.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                payload.Message = msg.GetString() ?? string.Empty;
            if (element.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                payload.Title = title.GetString() ?? string.Empty;
            if (element.TryGetProperty("notificationId", out var nid) && nid.ValueKind == JsonValueKind.String)
                payload.NotificationId = nid.GetString();
            if (element.TryGetProperty("isUrgent", out var urgent) && urgent.ValueKind == JsonValueKind.True)
                payload.IsUrgent = true;
            if (element.TryGetProperty("buttons", out var btns) && btns.ValueKind == JsonValueKind.Array)
            {
                foreach (var btn in btns.EnumerateArray())
                {
                    var text = btn.TryGetProperty("text", out var t) ? t.GetString() : null;
                    var action = btn.TryGetProperty("action", out var a) ? a.GetString() : null;
                    if (text != null && action != null)
                        payload.Buttons.Add(new NotificationButton { Text = text, Action = action });
                }
            }
        }

        private static void ParseFlat(JsonElement element, NotificationPayload payload)
        {
            if (element.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                payload.Message = msg.GetString() ?? string.Empty;
            if (element.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                payload.Title = title.GetString() ?? string.Empty;
            if (element.TryGetProperty("notificationId", out var nid) && nid.ValueKind == JsonValueKind.String)
                payload.NotificationId = nid.GetString();
            if (element.TryGetProperty("isUrgent", out var urgent) && urgent.ValueKind == JsonValueKind.True)
                payload.IsUrgent = true;
            if (element.TryGetProperty("content", out var stringContent) && stringContent.ValueKind == JsonValueKind.String)
                payload.Message = stringContent.GetString() ?? payload.Message;
        }
    }

    public class NotificationButton
    {
        public string Text { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
