using System.Net;
using RaceDayMVC.Models;

namespace RaceDayMVC.Services
{
    // The only place in the MVC app that talks to the RaceDay API.
    public class ApiService
    {
        private readonly HttpClient _http;
        private readonly IHttpContextAccessor _accessor;

        public ApiService(HttpClient http, IHttpContextAccessor accessor)
        {
            _http = http;
            _accessor = accessor;
        }

        // The API has separate role URLs: api/participants/... and api/organizers/...
        private static string RoleSegment(string role)
        {
            return role == "Organiser" ? "organizers" : "participants";
        }

        // Builds a request and attaches the logged-in user's API session cookie (if there is one)
        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            var cookie = _accessor.HttpContext?.Session.GetString("ApiCookie");
            if (!string.IsNullOrEmpty(cookie))
            {
                request.Headers.Add("Cookie", cookie);
            }

            return request;
        }

        // The API sends plain-text error messages; anything else gets a friendly default
        private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("{"))
            {
                return "Something went wrong. Please check your details and try again.";
            }

            return text.Trim('"');
        }

        // GET: returns the data, or the default value when the API says 404
        public async Task<T?> GetAsync<T>(string url)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new SessionExpiredException();
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>();
        }

        // Enrols the logged-in participant in an event under the chosen category
        public async Task<ApiResult> EnrolAsync(int eventId, int categoryId)
        {
            using var request = CreateRequest(HttpMethod.Post, "api/enrolment");
            request.Content = JsonContent.Create(new { eventID = eventId, categoryID = categoryId });

            using var response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new SessionExpiredException();
            }

            if (response.IsSuccessStatusCode)
            {
                return new ApiResult { Success = true };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new ApiResult { Error = "Only participants can enrol in events." };
            }

            return new ApiResult { Error = await ReadErrorAsync(response) };
        }

        // Registers a new Participant or Organiser account
        public async Task<ApiResult> RegisterAsync(RegisterViewModel model)
        {
            var body = new
            {
                firstName = model.FirstName.Trim(),
                lastName = model.LastName.Trim(),
                email = model.Email.Trim(),
                password = model.Password,
                role = model.Role
            };

            using var response = await _http.PostAsJsonAsync(
                $"api/{RoleSegment(model.Role)}/register", body);

            if (response.IsSuccessStatusCode)
            {
                return new ApiResult { Success = true };
            }

            return new ApiResult { Error = await ReadErrorAsync(response) };
        }

        // Logs in through the API, then keeps the API's session cookie and the user details in MVC's session
        public async Task<ApiResult> LoginAsync(LoginViewModel model)
        {
            var body = new
            {
                email = model.Email.Trim(),
                password = model.Password,
                role = model.Role
            };

            using var response = await _http.PostAsJsonAsync(
                $"api/{RoleSegment(model.Role)}/login", body);

            if (!response.IsSuccessStatusCode)
            {
                var error = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Invalid email or password."
                    : await ReadErrorAsync(response);

                return new ApiResult { Error = error };
            }

            string? apiCookie = null;
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                var sessionCookie = cookies.FirstOrDefault(c => c.StartsWith(".RaceDay.Session="));
                apiCookie = sessionCookie?.Split(';')[0];
            }

            var user = await response.Content.ReadFromJsonAsync<LoginResponse>();

            if (apiCookie == null || user == null)
            {
                return new ApiResult { Error = "Login failed. Please try again." };
            }

            // The password is never stored; only the API cookie and basic user details
            var session = _accessor.HttpContext!.Session;
            session.Clear();
            session.SetString("ApiCookie", apiCookie);
            session.SetString("UserId", user.UserId.ToString());
            session.SetString("FirstName", user.FirstName);
            session.SetString("Role", user.Role);

            return new ApiResult { Success = true };
        }

        // Ends the API session, then clears MVC's session
        public async Task LogoutAsync()
        {
            var session = _accessor.HttpContext?.Session;
            var role = session?.GetString("Role");

            if (session != null && !string.IsNullOrEmpty(role))
            {
                try
                {
                    using var request = CreateRequest(HttpMethod.Post, $"api/{RoleSegment(role)}/logout");
                    using var response = await _http.SendAsync(request);
                }
                catch (HttpRequestException)
                {
                    // If the API cannot be reached, the MVC session is still ended below
                }
            }

            session?.Clear();
        }
    }
}