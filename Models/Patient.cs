using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public required string FirstName { get; set; }

        [EmailAddress]
        public required string Email { get; set; }

        public string? Phone { get; set; }
        public required string PasswordHash { get; set; }  // Вместо Password
        public string? AvatarUrl { get; set; }

        // Every issued JWT carries this version. Incrementing it invalidates every
        // previously issued token for the patient immediately (logout/password change).
        public int TokenVersion { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}