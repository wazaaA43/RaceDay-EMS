namespace RaceDayMVC.Models
{
    public class PersonModel
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }

    public class OrganiserEnrolmentModel
    {
        public int EnrolmentID { get; set; }

        public int EventID { get; set; }

        public string Status { get; set; } = string.Empty;

        public PersonModel? Participant { get; set; }

        public CategoryModel? Category { get; set; }

        public ResultModel? Result { get; set; }
    }

    public class CategoriesViewModel
    {
        public EventModel Event { get; set; } = new EventModel();

        public List<CategoryModel> Categories { get; set; } = new List<CategoryModel>();
    }

    public class EnrolmentsViewModel
    {
        public EventModel Event { get; set; } = new EventModel();

        public List<OrganiserEnrolmentModel> Enrolments { get; set; } = new List<OrganiserEnrolmentModel>();
    }
}