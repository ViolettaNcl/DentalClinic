using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class ChangePasswordRequest
    {
        [Required]
        [StringLength(512, MinimumLength = 1)]
        public required string CurrentPassword { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Новый пароль должен содержать не менее 6 символов")]
        public required string NewPassword { get; set; }
    }
}
