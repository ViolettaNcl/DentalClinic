namespace DentalClinic.Models
{
    public class RegisterRequest
    {
        public required string FirstName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
