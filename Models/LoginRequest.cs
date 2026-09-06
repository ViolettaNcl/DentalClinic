using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class LoginRequest
    {
        [Required]
        [StringLength(320)]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(512, MinimumLength = 1)]
        public required string Password { get; set; }
    }
}
