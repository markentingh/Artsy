using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class SaveCollectionDraftAnswer
    {
        [JsonPropertyName("questionId")]
        public Guid? QuestionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid? ItemId { get; set; }

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = "";
    }

    public class SaveCollectionDraftRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("answers")]
        public List<SaveCollectionDraftAnswer> Answers { get; set; } = new();
    }
}
