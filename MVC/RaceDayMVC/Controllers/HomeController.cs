using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Models;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApiService _api;

        public HomeController(ApiService api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(
    string? search, string? type, string? location, DateTime? dateFrom, DateTime? dateTo)
        {
            var model = new EventFilterViewModel
            {
                Search = search,
                Type = type,
                Location = location,
                DateFrom = dateFrom,
                DateTo = dateTo
            };

            try
            {
                var all = await _api.GetAsync<List<EventModel>>("api/event/upcoming")
                    ?? new List<EventModel>();

                model.TotalUpcoming = all.Count;

                IEnumerable<EventModel> query = all;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(e => e.EventName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(e => string.Equals(e.EventType, type, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(location))
                {
                    query = query.Where(e => e.Location.Contains(location.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (dateFrom.HasValue)
                {
                    query = query.Where(e => e.EventDate.Date >= dateFrom.Value.Date);
                }

                if (dateTo.HasValue)
                {
                    query = query.Where(e => e.EventDate.Date <= dateTo.Value.Date);
                }

                model.Events = query.OrderBy(e => e.EventDate).ToList();
            }
            catch (HttpRequestException)
            {
                ViewBag.ApiError = "We could not load events right now. Please try again shortly.";
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult HttpError(int code)
        {
            return View(code);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}