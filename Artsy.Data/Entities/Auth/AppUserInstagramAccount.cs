using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Artsy.Data.Entities.Auth
{
    [Table("AppUserInstagramAccounts")]
    public class AppUserInstagramAccount
    {
        public Guid Id { get; set; }
        public Guid AppUserId { get; set; }
        public string InstagramBusinessAccountId { get; set; } = "";
        public string MetaUserId { get; set; } = "";
        public string MetaAccessToken { get; set; } = "";
        public DateTime? MetaTokenExpiresAtUtc { get; set; }
        public string? Username { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
