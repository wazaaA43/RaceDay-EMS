using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Filters;
using RaceDayMVC.Models;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApiService _api;

        public EventsController(ApiService api)
        {
            _api = api;
        }

        // Public: anyone can view an event; only logged-in participants see the enrol form
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _api.GetAsync<EventModel>($"api/event/{id}");

                if (item == null)
                {
                    TempData["Error"] = "That event could not be found.";
                    return RedirectToAction("Index", "Home");
                }

                var categories = await _api.GetAsync<List<CategoryModel>>($"api/category/event/{id}");

                return View(new EventDetailsViewModel
                {
                    Event = item,
                    Categories = categories ?? new List<CategoryModel>()
                });
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "We could not load that event right now. Please try again shortly.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleRequired("Participant")]
        public async Task<IActionResult> Enrol(int eventId, int categoryId)
        {
            if (categoryId == 0)
            {
                TempData["Error"] = "Please choose a category before enrolling.";
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            try
            {
                var result = await _api.EnrolAsync(eventId, categoryId);

                if (result.Success)
                {
                    TempData["Success"] = "You are enrolled! Your enrolment is pending confirmation.";
                }
                else
                {
                    TempData["Error"] = result.Error;
                }
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "We could not reach RaceDay right now. Please try again shortly.";
            }

            return RedirectToAction(nameof(Details), new { id = eventId });
        }
    }
}