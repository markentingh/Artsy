using System.Text.Json.Serialization;

namespace Artsy.API.Models.Orders
{
    public class OrderItemAnswerRequest
    {
        [JsonPropertyName("questionId")]
        public Guid QuestionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid? ItemId { get; set; }

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = "";
    }

    public class SaveOrderItemAnswersRequest
    {
        [JsonPropertyName("answers")]
        public List<OrderItemAnswerRequest> Answers { get; set; } = new();
    }
}
