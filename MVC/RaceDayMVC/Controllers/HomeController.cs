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

        public async Task<IActionResult> Index()
        {
            try
            {
                var events = await _api.GetAsync<List<EventModel>>("api/event/upcoming");
                return View(events ?? new List<EventModel>());
            }
            catch (HttpRequestException)
            {
                ViewBag.ApiError = "We could not load events right now. Please try again shortly.";
                return View(new List<EventModel>());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}