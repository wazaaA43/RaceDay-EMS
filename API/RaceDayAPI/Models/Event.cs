namespace RaceDayAPI.Models
{
    public class Event
    {
        public int EventID { get; set; }

        public string EventName { get; set; }

        public string Description { get; set; }

        public DateTime EventDate { get; set; }

        public string Location { get; set; }

        public decimal Distance { get; set; }

        public string EventType { get; set; }

        public string? BannerUrl { get; set; }

        public int OrganizerID { get; set; }

        public Organizer Organizer { get; set; }

        public ICollection<Category> Categories { get; set; }

        public ICollection<Enrolment> Enrolments { get; set; }
    }
}