namespace RaceDayAPI.Models
{
    public class LoginRequest
    {
        public string Email { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }
    }

    public class CreateEnrolmentRequest
    {
        public int CategoryID { get; set; }
    }

    public class UpdateEnrolmentRequest
    {
        public int CategoryID { get; set; }
    }
}
