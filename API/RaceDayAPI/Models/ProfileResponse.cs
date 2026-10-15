namespace RaceDayAPI.Models
{
    // Safe information returned to a logged-in user.
    // Never include PasswordHash in this response.
    public class ProfileResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
