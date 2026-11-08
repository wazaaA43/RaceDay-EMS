namespace RaceDayMVC.Models
{
    public class CategoryModel
    {
        public int CategoryID { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string CategoryType { get; set; } = string.Empty;

        public int EventID { get; set; }
    }

    public class EventDetailsViewModel
    {
        public EventModel Event { get; set; } = new EventModel();

        public List<CategoryModel> Categories { get; set; } = new List<CategoryModel>();
    }
}