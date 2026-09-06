using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

public class ModerateReviewRequest
{
    // approved | rejected
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    // Обязательно при Status = "rejected"
    [StringLength(500)]
    public string? RejectionReason { get; set; }
}
