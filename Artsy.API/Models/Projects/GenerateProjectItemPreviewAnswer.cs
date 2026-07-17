using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class GenerateProjectItemPreviewAnswer
    {
        [JsonPropertyName("questionId")]
        public Guid QuestionId { get; set; }

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = "";
    }
}
