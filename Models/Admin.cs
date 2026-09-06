namespace DentalClinic.Models
{
    public class Admin
    {
        public int Id { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }  // Вместо Password
        public string? AvatarUrl { get; set; }

        // Every issued JWT carries this version. Incrementing it invalidates every
        // previously issued token for the administrator immediately.
        public int TokenVersion { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}