using Artsy.Data.Entities.Projects;

namespace Artsy.API.Models.Projects
{
    public class ProjectListItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Key { get; set; } = "";
        public string Color { get; set; } = "";
        public int Status { get; set; }
        public DateTime Created { get; set; }
        public List<string> Images { get; set; } = new();
    }
}
