using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class RegisterRequest
    {
        [Required]
        [StringLength(100)]
        public required string FirstName { get; set; }

        [Required]
        [StringLength(320)]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public required string Password { get; set; }
    }
}
