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
}
