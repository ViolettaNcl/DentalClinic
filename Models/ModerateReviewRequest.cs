namespace DentalClinic.Models;

public class ModerateReviewRequest
{
    // approved | rejected
    public string Status { get; set; } = null!;

    // Обязательно при Status = "rejected"
    public string? RejectionReason { get; set; }
}
