using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

public class CreateReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Text { get; set; } = null!;
}
