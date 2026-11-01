namespace RaceDayAPI.Models
{
    /// <summary>
    /// One published race result for a participant, with the number of finishers in that event.
    /// </summary>
    public class ParticipantResultResponse
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