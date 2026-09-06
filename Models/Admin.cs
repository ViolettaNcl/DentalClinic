using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models
{
    public class Admin
    {
        public int Id { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }  // Вместо Password

        // Keep a browser-facing cache-busted URL while storing the actual image in
        // SQL so serverless/container restarts cannot erase administrator avatars.
        public string? AvatarUrl { get; set; }
        public byte[]? AvatarData { get; set; }

        [StringLength(50)]
        public string? AvatarContentType { get; set; }

        // Every issued JWT carries this version. Incrementing it invalidates every
        // previously issued token for the administrator immediately.
        public int TokenVersion { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}