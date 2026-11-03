using System.ComponentModel.DataAnnotations;

namespace RaceDayMVC.Models
{
    public class EventFormViewModel
    {
        public int EventID { get; set; }

        [Required(ErrorMessage = "Please enter the event name.")]
        [Display(Name = "Event name")]
        public string EventName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a description.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose the date and time.")]
        [Display(Name = "Date and time")]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "Please enter the location.")]
        public string Location { get; set; } = string.Empty;

        [Range(0.01, 1000, ErrorMessage = "Distance must be more than 0 and no more than 1000 km.")]
        [Display(Name = "Distance (km)")]
        public decimal Distance { get; set; }

        [Required(ErrorMessage = "Please choose an event type.")]
        [Display(Name = "Event type")]
        public string EventType { get; set; } = "Run";

        [Display(Name = "Banner image (optional)")]
        public IFormFile? Banner { get; set; }

        public string? BannerUrl { get; set; }
    }
}