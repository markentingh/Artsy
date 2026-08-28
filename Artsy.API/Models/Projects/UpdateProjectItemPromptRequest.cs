namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemPromptRequest
    {
        public Guid ItemId { get; set; }
        public string? Prompt { get; set; }
        public string? OptionalPrompt { get; set; }
    }
}
