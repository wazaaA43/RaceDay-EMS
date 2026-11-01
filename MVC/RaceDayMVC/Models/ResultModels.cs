namespace RaceDayMVC.Models
{
    public class ResultModel
    {
        public int ResultID { get; set; }

        public int EnrolmentID { get; set; }

        public TimeSpan FinishTime { get; set; }

        public int Position { get; set; }

        public bool IsPublished { get; set; }
    }

    public class ResultsViewModel
    {
        public EventModel Event { get; set; } = new EventModel();

        public List<OrganiserEnrolmentModel> Rows { get; set; } = new List<OrganiserEnrolmentModel>();

        public bool IsConcluded { get; set; }
    }

    public class ParticipantResultModel
    {
        public int ResultID { get; set; }

        public string EventName { get; set; } = string.Empty;

        public DateTime EventDate { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public TimeSpan FinishTime { get; set; }

        public int Position { get; set; }

        public int TotalFinishers { get; set; }
    }
}