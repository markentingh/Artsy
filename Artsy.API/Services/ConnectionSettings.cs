using Microsoft.Extensions.Configuration;

namespace Artsy.API.Services
{
    public static class ConnectionSettings
    {
        public static string PrintifyClientId { get; private set; } = "";
        public static string PrintifySecretKey { get; private set; } = "";
        public static string PrintifyApiToken { get; private set; } = "";
        public static string MetaAppId { get; private set; } = "";
        public static string MetaAppSecret { get; private set; } = "";
        public static string TelegramBotToken { get; private set; } = "";
        public static string TelegramBotUsername { get; private set; } = "";
        public static string OpenAiApiKey { get; private set; } = "";

        public static void Initialize(IConfiguration configuration)
        {
            PrintifyClientId = configuration["Printify:ClientId"] ?? "";
            PrintifySecretKey = configuration["Printify:SecretKey"] ?? "";
            PrintifyApiToken = configuration["Printify:ApiToken"] ?? "";
            MetaAppId = configuration["Meta:AppId"] ?? "";
            MetaAppSecret = configuration["Meta:AppSecret"] ?? "";
            TelegramBotToken = configuration["Telegram:BotToken"] ?? "";
            TelegramBotUsername = configuration["Telegram:BotUsername"] ?? "";
            OpenAiApiKey = configuration["OpenAI:ApiKey"] ?? "";
        }
    }
}
