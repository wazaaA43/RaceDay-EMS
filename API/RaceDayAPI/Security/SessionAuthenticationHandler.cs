using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RaceDayAPI.Security
{
    // Reads the logged-in user's id and role from the SERVER-SIDE session (stored on the server,
    // the browser only holds a session id cookie) and turns them into the claims that
    // [Authorize] and [Authorize(Roles = "...")] check.
    public class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "SessionAuth";

        public SessionAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            await Context.Session.LoadAsync();

            var userId = Context.Session.GetString("UserId");
            var role = Context.Session.GetString("Role");

            // No session data means the user is not logged in (results in 401 on protected endpoints)
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                return AuthenticateResult.NoResult();
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, Context.Session.GetString("FirstName") ?? string.Empty),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

            return AuthenticateResult.Success(ticket);
        }
    }
}