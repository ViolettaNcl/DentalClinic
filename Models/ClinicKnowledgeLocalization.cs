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

    // New translations are drafts until explicitly verified by an administrator.
    // Only verified localizations are eligible for Denta AI context.
    public bool IsVerified { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
