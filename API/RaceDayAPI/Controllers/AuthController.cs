using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RaceDayAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<object> _passwordHasher = new();

        public AuthController(RaceDayDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/Auth/register
        // POST: api/participants/register
        // POST: api/organizers/register
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

            string token = GenerateJwtToken(userId, firstName, lastName, email, request.Role);
            return Ok(new
            {
                message = "Login successful.",
                userId,
                firstName,
                lastName,
                email,
                role = request.Role,
                token
            });
        }

        // JWTs are stateless: this endpoint confirms logout, but the client must discard its token.
        // The token remains valid until expiry unless server-side revocation is implemented.
        [Authorize(Roles = "Participant")]
        [HttpPost("/api/participants/logout")]
        public IActionResult LogoutParticipant() =>
            Ok(new { message = "Logout successful. Discard the token on the client." });

        [Authorize(Roles = "Organiser")]
        [HttpPost("/api/organizers/logout")]
        public IActionResult LogoutOrganizer() =>
            Ok(new { message = "Logout successful. Discard the token on the client." });

        private string GenerateJwtToken(int userId, string firstName, string lastName, string email, string role)
        {
            string jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT signing key is missing from configuration.");
            string issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("JWT issuer is missing from configuration.");
            string audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("JWT audience is missing from configuration.");

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, firstName),
                new Claim(ClaimTypes.GivenName, firstName),
                new Claim(ClaimTypes.Surname, lastName),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(_configuration["Jwt:ExpiryMinutes"] ?? "60")),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
