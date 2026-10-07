namespace RaceDayAPI.Models
{
    public class Enrolment
    {
        public int EnrolmentID { get; set; }

        public int ParticipantID { get; set; }

        public int EventID { get; set; }

        public int CategoryID { get; set; }

        public Participant Participant { get; set; }

        public Event Event { get; set; }

        public Category Category { get; set; }

        public Result Result { get; set; }
    }
}