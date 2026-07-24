namespace DentalClinic.Models;

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string Text { get; set; } = null!;
}