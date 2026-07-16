using System.Text;
using System.Text.Json;
using Artsy.API.Models.Telegram;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Services
{
    public interface ITelegramService
    {
        Task SendMessage(string chatId, string text);
        Task Reply(TelegramMessage message);
    }

    public class TelegramService : ITelegramService
    {
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAppUserRepository _userRepository;
        const string BaseUrl = "https://api.telegram.org/bot";

        public TelegramService(IHttpClientFactory httpClientFactory, IAppUserRepository userRepository)
        {
            _httpClientFactory = httpClientFactory;
            _userRepository = userRepository;
        }

        public async Task SendMessage(string chatId, string text)
        {
            await Post("sendMessage", new
            {
                chat_id = chatId,
                text
            });
        }

        public async Task Reply(TelegramMessage message)
        {
            if (message?.Chat == null || message.From == null)
                return;

            var chatId = message.Chat.Id.ToString();
            var telegramUserId = message.From.Id.ToString();
            var text = message.Text ?? "";
            var messageId = message.MessageId;

            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                if (text.StartsWith("/start "))
                {
                    var token = text.Substring("/start ".Length).Trim();
                    var user = await _userRepository.FindByTelegramConnectionTokenAsync(token);
                    if (user != null)
                    {
                        user.TelegramUserId = telegramUserId;
                        user.TelegramChatId = chatId;
                        user.TelegramConnectionToken = null;
                        _userRepository.UpdateTelegramConnection(user);
                        await Post("sendMessage", new
                        {
                            chat_id = chatId,
                            text = $"Connected to Artsy as {user.FullName}. Reply here to send messages back to the app.",
                            reply_to_message_id = messageId
                        });
                    }
                }
                else
                {
                    var user = await _userRepository.FindByTelegramUserIdAsync(telegramUserId);
                    if (user != null)
                    {
                        await Post("sendMessage", new
                        {
                            chat_id = chatId,
                            text = $"Message received from {user.FullName}.",
                            reply_to_message_id = messageId
                        });
                    }
                }
            }
            catch
            {
                // Fire-and-forget: failures are intentionally not surfaced to Telegram.
            }
        }

        private async Task Post(string method, object payload)
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}{ConnectionSettings.TelegramBotToken}/{method}", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
