namespace RaceDayAPI.Models
{
    public class Participant
    {
        public int ParticipantID { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public ICollection<Enrolment> Enrolments { get; set; }
    }
}