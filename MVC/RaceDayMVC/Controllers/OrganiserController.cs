using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Filters;
using RaceDayMVC.Models;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    [RoleRequired("Organiser")]
    public class OrganiserController : Controller
    {
        private readonly ApiService _api;

        public OrganiserController(ApiService api)
        {
            _api = api;
        }

        // The shape the API expects when creating or updating an event
        private static object ToRequest(EventFormViewModel model)
        {
            return new
            {
                eventName = model.EventName.Trim(),
                description = model.Description.Trim(),
                eventDate = model.EventDate,
                location = model.Location.Trim(),
                distance = model.Distance,
                eventType = model.EventType
            };
        }

        public async Task<IActionResult> Events()
        {
            try
            {
                var list = await _api.GetAsync<List<EventModel>>("api/organizer/events");
                return View(list ?? new List<EventModel>());
            }
            catch (HttpRequestException)
            {
                ViewBag.ApiError = "We could not load your events right now. Please try again shortly.";
                return View(new List<EventModel>());
            }
        }

        [HttpGet]
        public IActionResult CreateEvent()
        {
            return View(new EventFormViewModel { EventDate = DateTime.Today.AddDays(30).AddHours(8) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(EventFormViewModel model)
        {
            if (model.EventDate <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(model.EventDate), "The event date must be in the future.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var result = await _api.PostAsync("api/event", ToRequest(model));

                if (!result.Success)
                {
                    ModelState.AddModelError(string.Empty, result.Error);
                    return View(model);
                }
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "We could not reach RaceDay right now. Please try again shortly.");
                return View(model);
            }

            TempData["Success"] = "Event created.";
            return RedirectToAction(nameof(Events));
        }

        [HttpGet]
        public async Task<IActionResult> EditEvent(int id)
        {
            try
            {
                var item = await _api.GetAsync<EventModel>($"api/organizer/events/{id}");

                if (item == null)
                {
                    TempData["Error"] = "That event could not be found.";
                    return RedirectToAction(nameof(Events));
                }

                return View(new EventFormViewModel
                {
                    EventID = item.EventID,
                    EventName = item.EventName,
                    Description = item.Description,
                    EventDate = item.EventDate,
                    Location = item.Location,
                    Distance = item.Distance,
                    EventType = item.EventType
                });
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "We could not load that event right now. Please try again shortly.";
                return RedirectToAction(nameof(Events));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(EventFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var result = await _api.PutAsync($"api/event/{model.EventID}", ToRequest(model));

                if (!result.Success)
                {
                    ModelState.AddModelError(string.Empty, result.Error);
                    return View(model);
                }
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "We could not reach RaceDay right now. Please try again shortly.");
                return View(model);
            }

            TempData["Success"] = "Event updated.";
            return RedirectToAction(nameof(Events));
        }

        [HttpGet]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                var item = await _api.GetAsync<EventModel>($"api/organizer/events/{id}");

                if (item == null)
                {
                    TempData["Error"] = "That event could not be found.";
                    return RedirectToAction(nameof(Events));
                }

                return View(item);
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "We could not load that event right now. Please try again shortly.";
                return RedirectToAction(nameof(Events));
            }
        }

        [HttpPost, ActionName("DeleteEvent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEventConfirmed(int id)
        {
            try
            {
                var result = await _api.DeleteAsync($"api/event/{id}");

                if (result.Success)
                {
                    TempData["Success"] = "Event deleted.";
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

            return RedirectToAction(nameof(Events));
        }
    }
}