using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DailyQuest.Services;

internal static class WebhookService
{
    private const int TimeoutSeconds = 10;
    private static readonly HttpClient _http = new();

    public static bool IsEnabled => Plugin.WebhookEnabled?.Value ?? false;
    public static bool IsConfigured() => !string.IsNullOrWhiteSpace(Plugin.WebhookUrl?.Value);

    /*
    public static bool SetEnabled(bool enabled, out string error)
    {
        try
        {
            Plugin.WebhookEnabled.Value = enabled;
            Plugin.PluginConfig.Save();
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            Core.LogException(e);
            return false;
        }
    }
    */

    public static async Task<(bool ok, string error)> SendAsync(string message, CancellationToken ct = default)
    {
        try
        {
            if (!IsEnabled)
                return (false, "Webhook is disabled.");

            string url = Plugin.WebhookUrl.Value;
            if (string.IsNullOrWhiteSpace(url))
                return (false, "Webhook URL is empty.");

            message = (message ?? "").Trim();
            if (message.Length == 0)
                return (false, "Message is empty.");

            if (message.Length > 1990)
                message = message[..1990] + "...";

            var payload = new
            {
                content = message,
                allowed_mentions = new { parse = Array.Empty<string>() }
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            var body = JsonSerializer.Serialize(payload);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, linkedCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (false, $"Discord returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
            }

            return (true, null);
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return (false, e.Message);
        }
    }
}
