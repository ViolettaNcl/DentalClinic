using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class UpdateProfileRequest
    {
        [StringLength(100, MinimumLength = 2)]
        public string? FirstName { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^[\d\s\+\-\(\)]{5,20}$", ErrorMessage = "Некорректный формат телефона")]
        public string? Phone { get; set; }
    }
}