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
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<object> _passwordHasher;

        public AuthController(
            RaceDayDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<object>();
        }

        // POST: api/Auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (request.Role != "Participant" &&
                request.Role != "Organiser")
            {
                return BadRequest("Role must be Participant or Organiser.");
            }

            if (request.Role == "Participant")
            {
                var existingParticipant = await _context.Participants
                    .FirstOrDefaultAsync(p => p.Email == request.Email);

                if (existingParticipant != null)
                {
                    return BadRequest("A participant with this email already exists.");
                }

                var participant = new Participant
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email
                };

                participant.PasswordHash =
                    _passwordHasher.HashPassword(participant, request.Password);

                _context.Participants.Add(participant);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Participant registered successfully."
                });
            }

            var existingOrganizer = await _context.Organizers
                .FirstOrDefaultAsync(o => o.Email == request.Email);

            if (existingOrganizer != null)
            {
                return BadRequest("An organiser with this email already exists.");
            }

            var organizer = new Organizer
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email
            };

            organizer.PasswordHash =
                _passwordHasher.HashPassword(organizer, request.Password);

            _context.Organizers.Add(organizer);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Organiser registered successfully."
            });
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (request.Role != "Participant" &&
                request.Role != "Organiser")
            {
                return BadRequest("Role must be Participant or Organiser.");
            }

            int userId;
            string firstName;
            string lastName;
            string email;
            string passwordHash;

            if (request.Role == "Participant")
            {
                var participant = await _context.Participants
                    .FirstOrDefaultAsync(p => p.Email == request.Email);

                if (participant == null)
                {
                    return Unauthorized("Invalid email or password.");
                }

                var passwordResult = _passwordHasher.VerifyHashedPassword(
                    participant,
                    participant.PasswordHash,
                    request.Password);

                if (passwordResult == PasswordVerificationResult.Failed)
                {
                    return Unauthorized("Invalid email or password.");
                }

                userId = participant.ParticipantID;
                firstName = participant.FirstName;
                lastName = participant.LastName;
                email = participant.Email;
                passwordHash = participant.PasswordHash;
            }
            else
            {
                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.Email == request.Email);

                if (organizer == null)
                {
                    return Unauthorized("Invalid email or password.");
                }

                var passwordResult = _passwordHasher.VerifyHashedPassword(
                    organizer,
                    organizer.PasswordHash,
                    request.Password);

                if (passwordResult == PasswordVerificationResult.Failed)
                {
                    return Unauthorized("Invalid email or password.");
                }

                userId = organizer.OrganizerID;
                firstName = organizer.FirstName;
                lastName = organizer.LastName;
                email = organizer.Email;
                passwordHash = organizer.PasswordHash;
            }

            var token = GenerateJwtToken(
                userId,
                firstName,
                lastName,
                email,
                request.Role);

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

        private string GenerateJwtToken(
            int userId,
            string firstName,
            string lastName,
            string email,
            string role)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, firstName),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(
                        _configuration["Jwt:ExpiryMinutes"])),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
