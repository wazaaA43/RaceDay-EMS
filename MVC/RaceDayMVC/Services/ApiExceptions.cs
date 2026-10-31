namespace RaceDayMVC.Services
{
    // Thrown when the API says the login session is no longer valid (for example after the API restarted)
    public class SessionExpiredException : Exception
    {
        public SessionExpiredException() : base("The API session has expired.")
        {
        }
    }
}