using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDayMVC.Services;

namespace RaceDayMVC.Filters
{
    // Put on a controller or action to require a logged-in user with a specific role.
    public class RoleRequiredAttribute : ActionFilterAttribute
    {
        private readonly string _role;

        public RoleRequiredAttribute(string role)
        {
            _role = role;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var role = context.HttpContext.Session.GetString("Role");
            var controller = context.Controller as Controller;

            if (role == null)
            {
                if (controller != null)
                {
                    controller.TempData["Error"] = "Please log in to continue.";
                }

                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (role != _role)
            {
                if (controller != null)
                {
                    controller.TempData["Error"] = "You do not have access to that page.";
                }

                context.Result = new RedirectToActionResult("Index", "Home", null);
            }
        }

        // If the API session has expired, end the MVC session too and send the user to log in again
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is SessionExpiredException)
            {
                context.HttpContext.Session.Clear();

                if (context.Controller is Controller controller)
                {
                    controller.TempData["Error"] = "Your session has expired. Please log in again.";
                }

                context.ExceptionHandled = true;
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}