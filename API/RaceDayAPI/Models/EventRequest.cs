namespace RaceDayAPI.Models
{
    /// <summary>
    /// The details an organiser sends when creating or updating an event.
    /// </summary>
    public class EventRequest
    {
        public string EventName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime EventDate { get; set; }

        public string Location { get; set; } = string.Empty;

        public decimal Distance { get; set; }

        public string EventType { get; set; } = string.Empty;
    }
}