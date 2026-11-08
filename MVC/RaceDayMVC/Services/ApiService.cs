using System.Net;
using System.Net.Http.Headers;
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

        // Sends a request. If the API does not answer in time, this throws an HttpRequestException,
        // which every controller already catches to show a friendly message instead of a crash.
        // Only a real timeout is converted; any other cancellation is passed on unchanged.
        private async Task<HttpResponseMessage> SendWithTimeoutAsync(HttpRequestMessage request)
        {
            try
            {
                return await _http.SendAsync(request);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new HttpRequestException("The RaceDay API did not respond in time.", ex);
            }
        }

        // Sends a JSON body with POST (used by register and login) with the same timeout handling
        private async Task<HttpResponseMessage> PostJsonAsync(string url, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body)
            };

            return await SendWithTimeoutAsync(request);
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
            using var response = await SendWithTimeoutAsync(request);

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

            using var response = await SendWithTimeoutAsync(request);

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

        // PUT: sends the changes to the API and reports whether it accepted them
        public async Task<ApiResult> PutAsync(string url, object body)
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            request.Content = JsonContent.Create(body);

            using var response = await SendWithTimeoutAsync(request);

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
                return new ApiResult { Error = "You do not have permission to do that." };
            }

            return new ApiResult { Error = await ReadErrorAsync(response) };
        }

        // POST: sends a new record to the API and reports whether it accepted it
        public async Task<ApiResult> PostAsync(string url, object body)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            request.Content = JsonContent.Create(body);

            using var response = await SendWithTimeoutAsync(request);

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
                return new ApiResult { Error = "You do not have permission to do that." };
            }

            return new ApiResult { Error = await ReadErrorAsync(response) };
        }

        // DELETE: asks the API to delete a record and reports whether it did
        public async Task<ApiResult> DeleteAsync(string url)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            using var response = await SendWithTimeoutAsync(request);

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
                return new ApiResult { Error = "You do not have permission to do that." };
            }

            return new ApiResult { Error = await ReadErrorAsync(response) };
        }

        // Creates an event and returns the new event's ID so a banner can be uploaded to it
        public async Task<(ApiResult Result, int EventId)> CreateEventAsync(object body)
        {
            using var request = CreateRequest(HttpMethod.Post, "api/event");
            request.Content = JsonContent.Create(body);

            using var response = await SendWithTimeoutAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new SessionExpiredException();
            }

            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<EventModel>();
                return (new ApiResult { Success = true }, created?.EventID ?? 0);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (new ApiResult { Error = "You do not have permission to do that." }, 0);
            }

            return (new ApiResult { Error = await ReadErrorAsync(response) }, 0);
        }

        // Sends an image to the API (the API checks the type and size and stores it in Azure Blob Storage)
        public async Task<ApiResult> UploadImageAsync(string url, IFormFile file)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var fileContent = new StreamContent(stream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

            content.Add(fileContent, "file", file.FileName);
            request.Content = content;

            using var response = await SendWithTimeoutAsync(request);

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
                return new ApiResult { Error = "You do not have permission to do that." };
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

            using var response = await PostJsonAsync(
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

            using var response = await PostJsonAsync(
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
                    using var response = await SendWithTimeoutAsync(request);
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