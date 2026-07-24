namespace DentalClinic.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public required string FirstName { get; set; }
        public required string Email { get; set; }
        public string? Phone { get; set; }
        public required string PasswordHash { get; set; }  // Вместо Password
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}