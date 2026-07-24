namespace Artsy.API.Models.Projects
{
    public class ArchiveProjectRequest
    {
        public Guid ProjectId { get; set; }
    }

    public class DeleteCollectionRequest
    {
        public Guid Id { get; set; }
    }
}
