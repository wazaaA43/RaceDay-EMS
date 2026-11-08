namespace RaceDayAPI.Models
{
    /// <summary>
    /// The details an organiser sends when creating a category for one of their events.
    /// </summary>
    public class CreateCategoryRequest
    {
        public string CategoryName { get; set; } = string.Empty;

        public string CategoryType { get; set; } = string.Empty;

        public int EventID { get; set; }
    }

    /// <summary>
    /// The details an organiser sends when renaming a category. The event cannot be changed.
    /// </summary>
    public class UpdateCategoryRequest
    {
        public string CategoryName { get; set; } = string.Empty;

        public string CategoryType { get; set; } = string.Empty;
    }
}