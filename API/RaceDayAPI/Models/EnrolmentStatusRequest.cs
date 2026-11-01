namespace RaceDayAPI.Models
{
    /// <summary>
    /// The new status an organiser sets on an enrolment ("Pending" or "Confirmed").
    /// </summary>
    public class UpdateEnrolmentStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}