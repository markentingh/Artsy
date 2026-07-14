using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Auth.Policies;

namespace Artsy.API.Controllers.Admin
{
    [Route("/api/admin/telegram")]
    [Authorize(Policy = nameof(AuthConstants.Policy.ManageUsers))]
    public class TelegramController : ApiController
    {
        readonly IHttpClientFactory _httpClientFactory;

        public TelegramController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("webhook-info")]
        public async Task<IActionResult> GetWebhookInfo()
        {
            if (string.IsNullOrEmpty(ConnectionSettings.TelegramBotToken))
                return Json(new ApiResponse { success = false, message = "Telegram bot is not configured." });

            try
            {
                var info = await GetWebhookInfoFromTelegram();
                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        url = info?.Result?.Url ?? "",
                        maxConnections = info?.Result?.MaxConnections ?? 0
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("set-webhook")]
        public async Task<IActionResult> SetWebhook([FromBody] SetWebhookRequest request)
        {
            if (string.IsNullOrEmpty(ConnectionSettings.TelegramBotToken))
                return Json(new ApiResponse { success = false, message = "Telegram bot is not configured." });

            if (string.IsNullOrEmpty(request.Url))
                return Json(new ApiResponse { success = false, message = "Webhook URL is required." });

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"https://api.telegram.org/bot{ConnectionSettings.TelegramBotToken}/setWebhook?url={Uri.EscapeDataString(request.Url)}");
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TelegramApiResponse>(json);

                if (result?.Ok != true)
                    return Json(new ApiResponse { success = false, message = "Telegram failed to set webhook.", data = result });

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<TelegramWebhookInfoResponse?> GetWebhookInfoFromTelegram()
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://api.telegram.org/bot{ConnectionSettings.TelegramBotToken}/getWebhookInfo");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TelegramWebhookInfoResponse>(json);
        }
    }

    public class SetWebhookRequest
    {
        public string Url { get; set; } = "";
    }

    public class TelegramWebhookInfoResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public TelegramWebhookInfoResult? Result { get; set; }
    }

    public class TelegramWebhookInfoResult
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("has_custom_certificate")]
        public bool HasCustomCertificate { get; set; }

        [JsonPropertyName("pending_update_count")]
        public int PendingUpdateCount { get; set; }

        [JsonPropertyName("max_connections")]
        public int MaxConnections { get; set; }

        [JsonPropertyName("allowed_updates")]
        public List<string> AllowedUpdates { get; set; } = new List<string>();
    }

    public class TelegramApiResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public bool Result { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }
}
