namespace Artsy.API.Models.Projects
{
    public class UpdateProjectInstagramIdRequest
    {
        public Guid Id { get; set; }
        public Guid? InstagramId { get; set; }
    }
}
