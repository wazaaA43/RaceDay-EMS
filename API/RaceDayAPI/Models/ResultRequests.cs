namespace RaceDayAPI.Models
{
    /// <summary>
    /// The details an organiser sends when capturing a result for an enrolment.
    /// </summary>
    public class CreateResultRequest
    {
        public int EnrolmentID { get; set; }

        public TimeSpan FinishTime { get; set; }

        public int Position { get; set; }
    }

    /// <summary>
    /// The details an organiser sends when correcting an existing result.
    /// </summary>
    public class UpdateResultRequest
    {
        public TimeSpan FinishTime { get; set; }

        public int Position { get; set; }
    }
}