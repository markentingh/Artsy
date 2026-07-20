namespace Artsy.API.Models
{
    public class SendTelegramMessageRequest
    {
        public Guid UserId { get; set; }
        public string Text { get; set; } = "";
    }
}
