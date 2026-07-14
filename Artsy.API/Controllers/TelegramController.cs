using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Controllers
{
    [Route("/api/telegram")]
    public class TelegramController : ApiController
    {
        readonly IAppUserRepository _userRepository;
        readonly IHttpClientFactory _httpClientFactory;

        public TelegramController(IAppUserRepository userRepository, IHttpClientFactory httpClientFactory)
        {
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> Status()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var user = await _userRepository.FindByGuidAsync(userId);
                if (user == null)
                    return Json(new ApiResponse { success = false, message = "User not found" });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        connected = !string.IsNullOrEmpty(user.TelegramChatId),
                        telegramUserId = user.TelegramUserId,
                        telegramChatId = user.TelegramChatId
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("connect")]
        [Authorize]
        public async Task<IActionResult> Connect()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (string.IsNullOrEmpty(ConnectionSettings.TelegramBotToken) || string.IsNullOrEmpty(ConnectionSettings.TelegramBotUsername))
                return Json(new ApiResponse { success = false, message = "Telegram bot is not configured." });

            try
            {
                var user = await _userRepository.FindByGuidAsync(userId);
                if (user == null)
                    return Json(new ApiResponse { success = false, message = "User not found" });

                var token = GenerateConnectionToken();
                user.TelegramConnectionToken = token;
                _userRepository.UpdateTelegramConnection(user);

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        botUsername = ConnectionSettings.TelegramBotUsername,
                        token
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("/api/webhooks/telegram")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] Models.Telegram.TelegramUpdate update)
        {
            if (string.IsNullOrEmpty(ConnectionSettings.TelegramBotToken))
                return Ok();

            try
            {
                var message = update.Message;
                if (message?.Chat == null || message.From == null)
                    return Ok();

                var chatId = message.Chat.Id.ToString();
                var telegramUserId = message.From.Id.ToString();
                var text = message.Text ?? "";

                if (!string.IsNullOrEmpty(text) && text.StartsWith("/start "))
                {
                    var token = text.Substring("/start ".Length).Trim();
                    var user = await _userRepository.FindByTelegramConnectionTokenAsync(token);
                    if (user != null)
                    {
                        user.TelegramUserId = telegramUserId;
                        user.TelegramChatId = chatId;
                        user.TelegramConnectionToken = null;
                        _userRepository.UpdateTelegramConnection(user);
                        await SendTelegramMessage(chatId, $"Connected to Artsy as {user.FullName}. Reply here to send messages back to the app.");
                    }
                }
                else if (!string.IsNullOrEmpty(text))
                {
                    var user = await _userRepository.FindByTelegramUserIdAsync(telegramUserId);
                    if (user != null)
                    {
                        // Inbound message from a known user. Persist or process as needed.
                        await SendTelegramMessage(chatId, $"Message received from {user.FullName}.");
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message });
            }
        }

        [HttpPost("send")]
        [Authorize]
        public async Task<IActionResult> Send([FromBody] Models.SendTelegramMessageRequest request)
        {
            if (string.IsNullOrEmpty(ConnectionSettings.TelegramBotToken))
                return Json(new ApiResponse { success = false, message = "Telegram bot is not configured." });

            try
            {
                var user = await _userRepository.FindByGuidAsync(request.UserId);
                if (user == null)
                    return Json(new ApiResponse { success = false, message = "User not found" });

                if (string.IsNullOrEmpty(user.TelegramChatId))
                    return Json(new ApiResponse { success = false, message = "User has not connected Telegram." });

                await SendTelegramMessage(user.TelegramChatId, request.Text);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private static string GenerateConnectionToken()
        {
            return Guid.NewGuid().ToString();
        }

        private async Task SendTelegramMessage(string chatId, string text)
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(new
            {
                chat_id = chatId,
                text
            }), Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"https://api.telegram.org/bot{ConnectionSettings.TelegramBotToken}/sendMessage", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
