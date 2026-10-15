using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;

namespace RaceDayAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly PasswordHasher<object> _passwordHasher = new();

        // IConfiguration is kept in the constructor so the existing unit tests still compile
        public AuthController(RaceDayDbContext context, IConfiguration configuration)
        {
            _context = context;
        }

        // POST: api/Auth/register
        // POST: api/participants/register
        // POST: api/organizers/register
        /// <summary>
        /// Registers a new Participant or Organiser account. Also reachable as api/participants/register and api/organizers/register, which set the role automatically.
        /// </summary>
        /// <param name="request">First name, last name, email, password (minimum 8 characters) and role ("Participant" or "Organiser").</param>
        /// <returns>A confirmation message. The password is stored as a hash, never in plain text.</returns>
        /// <response code="201">Account created.</response>
        /// <response code="400">Missing details, short password, invalid role or email already in use.</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("register")]
        [HttpPost("/api/participants/register")]
        [HttpPost("/api/organizers/register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (Request.Path.StartsWithSegments("/api/participants"))
                request.Role = "Participant";
            else if (Request.Path.StartsWithSegments("/api/organizers"))
                request.Role = "Organiser";

            if (request.Role != "Participant" && request.Role != "Organiser")
                return BadRequest("Role must be Participant or Organiser.");

            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("First name, last name, email and password are required.");

            if (request.Password.Length < 8)
                return BadRequest("Password must be at least 8 characters.");

            string email = request.Email.Trim();

            bool participantEmailExists = await _context.Participants.AnyAsync(p => p.Email == email);
            bool organizerEmailExists = await _context.Organizers.AnyAsync(o => o.Email == email);
            if (participantEmailExists || organizerEmailExists)
                return BadRequest("This email address is already in use.");

            if (request.Role == "Participant")
            {
                var participant = new Participant
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = email
                };
                participant.PasswordHash = _passwordHasher.HashPassword(participant, request.Password);
                _context.Participants.Add(participant);
                await _context.SaveChangesAsync();
                return StatusCode(StatusCodes.Status201Created,
                    new { message = "Participant registered successfully." });
            }

            var organizer = new Organizer
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email
            };
            organizer.PasswordHash = _passwordHasher.HashPassword(organizer, request.Password);
            _context.Organizers.Add(organizer);
            await _context.SaveChangesAsync();
            return StatusCode(StatusCodes.Status201Created,
                new { message = "Organiser registered successfully." });
        }

        // POST: api/Auth/login
        // POST: api/participants/login
        // POST: api/organizers/login
        /// <summary>
        /// Logs in with email, password and role. On success the server creates a server-side session that stores the user id and role; the browser keeps only the session cookie, which is sent automatically on every later request.
        /// </summary>
        /// <param name="request">Email, password and role ("Participant" or "Organiser"). The role is set automatically on api/participants/login and api/organizers/login.</param>
        /// <returns>The logged-in user's details. The session is created on the server.</returns>
        /// <response code="200">Login successful, session started.</response>
        /// <response code="400">Missing details or invalid role.</response>
        /// <response code="401">Invalid email or password.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("login")]
        [HttpPost("/api/participants/login")]
        [HttpPost("/api/organizers/login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (Request.Path.StartsWithSegments("/api/participants"))
                request.Role = "Participant";
            else if (Request.Path.StartsWithSegments("/api/organizers"))
                request.Role = "Organiser";

            if (request.Role != "Participant" && request.Role != "Organiser")
                return BadRequest("Role must be Participant or Organiser.");

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            int userId;
            string firstName;
            string lastName;
            string email;
            string passwordHash;
            object userForHasher;

            if (request.Role == "Participant")
            {
                var participant = await _context.Participants.FirstOrDefaultAsync(p => p.Email == request.Email.Trim());
                if (participant == null)
                    return Unauthorized("Invalid email or password.");

                userForHasher = participant;
                passwordHash = participant.PasswordHash;
                var result = _passwordHasher.VerifyHashedPassword(userForHasher, passwordHash, request.Password);
                if (result == PasswordVerificationResult.Failed)
                    return Unauthorized("Invalid email or password.");

                userId = participant.ParticipantID;
                firstName = participant.FirstName;
                lastName = participant.LastName;
                email = participant.Email;
            }
            else
            {
                var organizer = await _context.Organizers.FirstOrDefaultAsync(o => o.Email == request.Email.Trim());
                if (organizer == null)
                    return Unauthorized("Invalid email or password.");

                userForHasher = organizer;
                passwordHash = organizer.PasswordHash;
                var result = _passwordHasher.VerifyHashedPassword(userForHasher, passwordHash, request.Password);
                if (result == PasswordVerificationResult.Failed)
                    return Unauthorized("Invalid email or password.");

                userId = organizer.OrganizerID;
                firstName = organizer.FirstName;
                lastName = organizer.LastName;
                email = organizer.Email;
            }

            // Start a fresh server-side session and store who is logged in and their role
            HttpContext.Session.Clear();
            HttpContext.Session.SetString("UserId", userId.ToString());
            HttpContext.Session.SetString("FirstName", firstName);
            HttpContext.Session.SetString("Role", request.Role);

            return Ok(new
            {
                message = "Login successful.",
                userId,
                firstName,
                lastName,
                email,
                role = request.Role
            });
        }

        /// <summary>
        /// Logs a Participant out by clearing their server-side session.
        /// </summary>
        /// <returns>A logout confirmation message.</returns>
        /// <response code="200">Logout confirmed.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Participant")]
        [HttpPost("/api/participants/logout")]
        public IActionResult LogoutParticipant()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "Logout successful. Your session has been ended." });
        }

        /// <summary>
        /// Logs an Organiser out by clearing their server-side session.
        /// </summary>
        /// <returns>A logout confirmation message.</returns>
        /// <response code="200">Logout confirmed.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Organiser")]
        [HttpPost("/api/organizers/logout")]
        public IActionResult LogoutOrganizer()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "Logout successful. Your session has been ended." });
        }
    }
}
