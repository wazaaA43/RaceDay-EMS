namespace RaceDayMVC.Models
{
    public class DashboardEventRow
    {
        public EventModel Event { get; set; } = new EventModel();

        public int TotalEnrolments { get; set; }

        public int PendingEnrolments { get; set; }

        public bool IsUpcoming { get; set; }

        public int DaysUntil { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalEvents { get; set; }

        public int UpcomingEvents { get; set; }

        public int TotalEnrolments { get; set; }

        public int PendingEnrolments { get; set; }

        public List<DashboardEventRow> Rows { get; set; } = new List<DashboardEventRow>();
    }
}