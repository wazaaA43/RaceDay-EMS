namespace RaceDayAPI.Models
{
    public class LoginRequest
    {
        public string Email { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }
    }
}
