using System.Net;

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

        // GET: returns the data, or the default value when the API says 404
        public async Task<T?> GetAsync<T>(string url)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>();
        }
    }
}