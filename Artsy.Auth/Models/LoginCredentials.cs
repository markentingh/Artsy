using System.ComponentModel.DataAnnotations;

namespace Artsy.Auth.Models
{
    public class LoginCredentials
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }
}
