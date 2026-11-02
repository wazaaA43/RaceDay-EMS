namespace RaceDayMVC.Models
{
    public class EventFilterViewModel
    {
        public List<EventModel> Events { get; set; } = new List<EventModel>();

        public int TotalUpcoming { get; set; }

        public string? Search { get; set; }

        public string? Type { get; set; }

        public string? Location { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(Type) ||
            !string.IsNullOrWhiteSpace(Location) ||
            DateFrom.HasValue ||
            DateTo.HasValue;
    }
}