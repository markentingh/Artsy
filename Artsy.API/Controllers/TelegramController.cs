using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        readonly ITelegramService _telegramService;

        public TelegramController(IAppUserRepository userRepository, ITelegramService telegramService)
        {
            _userRepository = userRepository;
            _telegramService = telegramService;
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
        public IActionResult Webhook([FromBody] Models.Telegram.TelegramUpdate update)
        {
            if (string.IsNullOrEmpty(ConnectionSettings.TelegramBotToken))
                return Ok();

            var message = update.Message;
            if (message?.Chat == null || message.From == null)
                return Ok();

            var text = message.Text ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                _ = _telegramService.Reply(message);
            }

            return Ok();
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

                await _telegramService.SendMessage(user.TelegramChatId, request.Text);
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

    }
}
