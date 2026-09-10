using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

[Table("ClinicKnowledgeLocalizations")]
public class ClinicKnowledgeLocalization
{
    [Key]
    public int Id { get; set; }

    public int ClinicKnowledgeItemId { get; set; }

    [Required, StringLength(2)]
    public string Lang { get; set; } = null!;

    [Required, StringLength(160)]
    public string Title { get; set; } = null!;

    [Required, StringLength(1200)]
    public string Content { get; set; } = null!;

    [StringLength(300)]
    public string? Keywords { get; set; }

    public bool IsVerified { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
