using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

[Table("ClinicKnowledgeItems")]
public class ClinicKnowledgeItem
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Category { get; set; } = null!;

    // Russian is the canonical/source text for this clinic knowledge row.
    // Other supported languages live in ClinicKnowledgeLocalizations and must be
    // explicitly supplied/verified by an administrator; Denta never stores an
    // automatically generated translation as a clinic fact.
    [Required, StringLength(160)]
    public string Title { get; set; } = null!;

    [Required, StringLength(1200)]
    public string Content { get; set; } = null!;

    [StringLength(300)]
    public string? Keywords { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClinicKnowledgeLocalization> Localizations { get; set; }
        = new List<ClinicKnowledgeLocalization>();
}
