using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class ChangePasswordRequest
    {
        [Required]
        [StringLength(512, MinimumLength = 1)]
        public required string CurrentPassword { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Новый пароль должен содержать не менее 8 символов")]
        public required string NewPassword { get; set; }
    }
}
