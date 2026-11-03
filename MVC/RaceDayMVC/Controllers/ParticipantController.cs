using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Filters;
using RaceDayMVC.Models;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    [RoleRequired("Participant")]
    public class ParticipantController : Controller
    {
        private readonly ApiService _api;

        public ParticipantController(ApiService api)
        {
            _api = api;
        }

        public async Task<IActionResult> MyEnrolments()
        {
            try
            {
                var list = await _api.GetAsync<List<EnrolmentModel>>("api/enrolment/my");

                var sorted = (list ?? new List<EnrolmentModel>())
                    .OrderBy(e => e.Event?.EventDate)
                    .ToList();

                return View(sorted);
            }
            catch (HttpRequestException)
            {
                ViewBag.ApiError = "We could not load your enrolments right now. Please try again shortly.";
                return View(new List<EnrolmentModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var profile = await _api.GetAsync<ProfileViewModel>("api/participant/profile");

                if (profile == null)
                {
                    TempData["Error"] = "Your profile could not be found.";
                    return RedirectToAction("Index", "Home");
                }

                return View(profile);
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "We could not load your profile right now. Please try again shortly.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var result = await _api.PutAsync("api/participant/profile", new
                {
                    firstName = model.FirstName.Trim(),
                    lastName = model.LastName.Trim(),
                    email = model.Email.Trim()
                });

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

            // Keep the name in the navigation bar up to date
            HttpContext.Session.SetString("FirstName", model.FirstName.Trim());

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile? picture)
        {
            if (picture == null || picture.Length == 0)
            {
                TempData["Error"] = "Please choose an image to upload.";
                return RedirectToAction(nameof(Profile));
            }

            try
            {
                var result = await _api.UploadImageAsync("api/participant/profile-picture", picture);

                if (result.Success)
                {
                    TempData["Success"] = "Profile picture updated.";
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

            return RedirectToAction(nameof(Profile));
        }

        public async Task<IActionResult> MyResults()
        {
            try
            {
                var list = await _api.GetAsync<List<ParticipantResultModel>>("api/participant/results");
                return View(list ?? new List<ParticipantResultModel>());
            }
            catch (HttpRequestException)
            {
                ViewBag.ApiError = "We could not load your results right now. Please try again shortly.";
                return View(new List<ParticipantResultModel>());
            }
        }
    }
}