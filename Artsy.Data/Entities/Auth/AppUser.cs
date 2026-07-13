using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Artsy.Data.Entities.Auth
{
    [Table("AppUsers")]
    public class AppUser
    {
        public Guid? Id { get; set; }
        [Required]
        public string FullName { get; set; } = "";
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
        public bool EmailConfirmed { get; set; } = false;
        public string? PasswordHash { get; set; } = string.Empty;
        public DateTime? LockoutEndDate { get; set; } = null;
        public bool LockoutEnabled { get; set; } = false;
        public int AccessFailedCount { get; set; } = 0;
        public DateTime? AccessFailedTime { get; set; } = null;
        public string PasswordResetHash { get; set; } = string.Empty;
        public DateTime? PasswordResetTime { get; set; } = null;
        public string NewEmail { get; set; } = string.Empty;
        public AppUserStatus Status { get; set; }
        public DateTime Created { get; set; }

        public string? PrintifyAccessToken { get; set; }
        public string? PrintifyRefreshToken { get; set; }
        public DateTime? PrintifyTokensExpireAtUtc { get; set; }
        public string? PrintifyShopId { get; set; }

        public string? MetaAccessToken { get; set; }
        public DateTime? MetaTokenExpiresAtUtc { get; set; }
        public string? MetaUserId { get; set; }
        public string? InstagramBusinessAccountId { get; set; }

        public string? TelegramUserId { get; set; }
        public string? TelegramChatId { get; set; }
        public string? TelegramConnectionToken { get; set; }

        public string? OAuthState { get; set; }

        public virtual ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
        public virtual ICollection<AppUserTokens> UserTokens { get; set; } = new List<AppUserTokens>();

        [NotMapped]
        public string password { get; set; } = "";
        [NotMapped]
        public bool IsAdmin { get; set; } = false;
        [NotMapped]
        public DateTime? LastLogin { get; set; }
    }

    public enum AppUserStatus
    {
        Pending,
        Active,
        Inactive,
        Deleted
    }
}
