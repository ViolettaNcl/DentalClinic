using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

public sealed class CreateAdminAccountRequest
{
    [Required]
    [StringLength(320)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    public bool IsSuperAdmin { get; set; }
}

public sealed class SetSuperAdminRequest
{
    public bool IsSuperAdmin { get; set; }
}

public sealed class ResetAdminPasswordRequest
{
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}