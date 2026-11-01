using System.ComponentModel.DataAnnotations;

namespace RaceDayMVC.Models
{
    public class EnrolmentModel
    {
        public int EnrolmentID { get; set; }

        public int EventID { get; set; }

        public int CategoryID { get; set; }

        public string Status { get; set; } = string.Empty;

        public EventModel? Event { get; set; }

        public CategoryModel? Category { get; set; }
    }

    public class ProfileViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter your first name.")]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your last name.")]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;
    }
}