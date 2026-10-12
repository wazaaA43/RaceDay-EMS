using Microsoft.Extensions.Logging;

namespace RaceDayAPI.Models
{
    public class Organizer
    {
        public int OrganizerID { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}