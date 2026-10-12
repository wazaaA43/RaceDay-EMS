using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using System.Security.Claims;

namespace RaceDayAPI.Controllers
{
    [ApiController]
    [Route("api/participants")]
    [Authorize(Roles = "Participant")]
    public class ParticipantController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly PasswordHasher<object> _passwordHasher = new();

        public ParticipantController(RaceDayDbContext context) => _context = context;

        private int GetParticipantId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int participantId) ? participantId : 0;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            int id = GetParticipantId();
            var participant = await _context.Participants.AsNoTracking()
                .Where(p => p.ParticipantID == id)
                .Select(p => new { p.ParticipantID, p.FirstName, p.LastName, p.Email })
                .FirstOrDefaultAsync();

            return participant == null
                ? NotFound(new { message = "Participant profile not found." })
                : Ok(participant);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateParticipantProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "First name, last name and email are required." });

            int id = GetParticipantId();
            var participant = await _context.Participants.FindAsync(id);
            if (participant == null)
                return NotFound(new { message = "Participant not found." });

            string email = request.Email.Trim();
            bool emailExists = await _context.Participants.AnyAsync(p => p.Email == email && p.ParticipantID != id)
                || await _context.Organizers.AnyAsync(o => o.Email == email);
            if (emailExists)
                return BadRequest(new { message = "This email address is already in use." });

            participant.FirstName = request.FirstName.Trim();
            participant.LastName = request.LastName.Trim();
            participant.Email = email;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Profile updated successfully." });
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword(ChangeParticipantPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Both passwords are required." });
            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "The new password must be at least 8 characters." });

            var participant = await _context.Participants.FindAsync(GetParticipantId());
            if (participant == null)
                return NotFound(new { message = "Participant not found." });

            var verification = _passwordHasher.VerifyHashedPassword(participant, participant.PasswordHash, request.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Current password is incorrect." });

            participant.PasswordHash = _passwordHasher.HashPassword(participant, request.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Password changed successfully." });
        }
    }

    public class UpdateParticipantProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ChangeParticipantPasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
