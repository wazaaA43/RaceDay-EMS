namespace RaceDayAPI.Models
{
    /// <summary>
    /// The personal details a participant or organiser can change on their own profile.
    /// </summary>
    public class ProfileUpdateRequest
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}