namespace RaceDayAPI.Models
{
    public class Category
    {
        public int CategoryID { get; set; }

        public string CategoryName { get; set; }

        public string CategoryType { get; set; }

        public int EventID { get; set; }

        public Event Event { get; set; }

        public ICollection<Enrolment> Enrolments { get; set; }
    }
}