using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

[Table("ClinicKnowledgeItemLocalizations")]
public class ClinicKnowledgeItemLocalization
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClinicKnowledgeItemId { get; set; }

    [Required, StringLength(10)]
    public string Language { get; set; } = null!;

    [Required, StringLength(160)]
    public string Title { get; set; } = null!;

    [Required, StringLength(1200)]
    public string Content { get; set; } = null!;

    public bool IsVerified { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
