using Azure.Core;
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

        public ParticipantController(RaceDayDbContext context)
        { 
            _context = context;
        }

        // Gets the ID of the currently authenticated participant.
        private int GetParticipantId() 
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int participantId) ? participantId : 0; 
        }

        // GET: api/participants/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            int participantId = GetParticipantId();

            var participant = await _context.Participants
                .Where(p => p.ParticipantID == participantId)
                .Select(p => new
                {
                    p.ParticipantID,
                    p.FirstName,
                    p.LastName,
                    p.Email
                })
                .FirstOrDefaultAsync();

            if (participant == null)
                return NotFound(new { message = "Participant profile not found." });

            return Ok(participant);
        }

        // PUT: api/participants/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateParticipantProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "First name, last name and email are required." });
            }

            int participantId = GetParticipantId();

            var participant = await _context.Participants.FindAsync(participantId);

            if (participant == null)
                return NotFound(new { message = "Participant not found." });

            bool emailExists = await _context.Participants
                .AnyAsync(p => p.Email == request.Email && p.ParticipantID != participantId);

            bool organiserEmailExists = await _context.Organizers
                .AnyAsync(o => o.Email == request.Email);

            if (emailExists || organiserEmailExists)
                return BadRequest(new { message = "This email address is already in use." });

            participant.FirstName = request.FirstName.Trim();
            participant.LastName = request.LastName.Trim();
            participant.Email = request.Email.Trim();

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully." });
        }

        // PUT: api/participants/password
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword(ChangeParticipantPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Both passwords are required." });
            }

            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "The new password must be at least 8 characters." });

            int participantId = GetParticipantId();

            var participant = await _context.Participants.FindAsync(participantId);

            if (participant == null)
                return NotFound(new { message = "Participant not found." });

            var verification = _passwordHasher.VerifyHashedPassword(
                new object(),
                participant.PasswordHash,
                request.CurrentPassword);

            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Current password is incorrect." });

            participant.PasswordHash = _passwordHasher.HashPassword(
                new object(),
                request.NewPassword);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully." });
        }

        // GET: api/events
        [HttpGet("/api/events")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEvents()
        {
            var events = await _context.Events
                .AsNoTracking()
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/events/{eventId}
        [HttpGet("/api/events/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEvent(int eventId)
        {
            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            return Ok(eventItem);
        }

        // GET: api/events/upcoming
        [HttpGet("/api/events/upcoming")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUpcomingEvents()
        {
            var events = await _context.Events
                .AsNoTracking()
                .Where(e => e.EventDate >= DateTime.Today)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/events/past
        [HttpGet("/api/events/past")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPastEvents()
        {
            var events = await _context.Events
                .AsNoTracking()
                .Where(e => e.EventDate < DateTime.Today)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/events/{eventId}/categories
        [HttpGet("/api/events/{eventId:int}/categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEventCategories(int eventId)
        {
            bool eventExists = await _context.Events
                .AnyAsync(e => e.EventID == eventId);

            if (!eventExists)
                return NotFound(new { message = "Event not found." });

            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.EventID == eventId)
                .ToListAsync();

            return Ok(categories);
        }

        // GET: api/categories/{categoryId}
        [HttpGet("/api/categories/{categoryId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategory(int categoryId)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryID == categoryId);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            return Ok(category);
        }

        // POST: api/events/{eventId}/enrolments
        [HttpPost("/api/events/{eventId:int}/enrolments")]
        public async Task<IActionResult> EnrolInEvent(
            int eventId, CreateEnrolmentRequest request)
        {
            int participantId = GetParticipantId();

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.EventDate < DateTime.Today)
                return BadRequest(new { message = "You cannot enrol in a past event." });

            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryID == request.CategoryID &&
                    c.EventID == eventId);

            if (category == null)
                return BadRequest(new { message = "The category does not belong to this event." });

            bool alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e => e.ParticipantID == participantId &&
                               e.EventID == eventId);

            if (alreadyEnrolled)
                return BadRequest(new { message = "You are already enrolled in this event." });

            var enrolment = new Enrolment
            {
                ParticipantID = participantId,
                EventID = eventId,
                CategoryID = request.CategoryID
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetMyEnrolment),
                new { enrolmentId = enrolment.EnrolmentID },
                enrolment);
        }

        // GET: api/participants/enrolments
        [HttpGet("enrolments")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            int participantId = GetParticipantId();

            var enrolments = await _context.Enrolments
                .AsNoTracking()
                .Where(e => e.ParticipantID == participantId)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .ToListAsync();

            return Ok(enrolments);
        }

        // GET: api/participants/enrolments/{enrolmentId}
        [HttpGet("enrolments/{enrolmentId:int}")]
        public async Task<IActionResult> GetMyEnrolment(int enrolmentId)
        {
            int participantId = GetParticipantId();

            var enrolment = await _context.Enrolments
                .AsNoTracking()
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .FirstOrDefaultAsync(e =>
                    e.EnrolmentID == enrolmentId &&
                    e.ParticipantID == participantId);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            return Ok(enrolment);
        }

        // PUT: api/participants/enrolments/{enrolmentId}
        [HttpPut("enrolments/{enrolmentId:int}")]
        public async Task<IActionResult> UpdateMyEnrolment(
            int enrolmentId, UpdateEnrolmentRequest request)
        {
            int participantId = GetParticipantId();

            var enrolment = await _context.Enrolments
                .FirstOrDefaultAsync(e =>
                    e.EnrolmentID == enrolmentId &&
                    e.ParticipantID == participantId);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryID == request.CategoryID &&
                    c.EventID == enrolment.EventID);

            if (category == null)
                return BadRequest(new { message = "The category does not belong to this event." });

            if (await _context.Results.AnyAsync(r => r.EnrolmentID == enrolmentId))
                return BadRequest(new { message = "An enrolment with a result cannot be changed." });

            enrolment.CategoryID = request.CategoryID;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Enrolment updated successfully." });
        }

        // DELETE: api/participants/enrolments/{enrolmentId}
        [HttpDelete("enrolments/{enrolmentId:int}")]
        public async Task<IActionResult> CancelMyEnrolment(int enrolmentId)
        {
            int participantId = GetParticipantId();

            var enrolment = await _context.Enrolments
                .FirstOrDefaultAsync(e =>
                    e.EnrolmentID == enrolmentId &&
                    e.ParticipantID == participantId);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (await _context.Results.AnyAsync(r => r.EnrolmentID == enrolmentId))
                return BadRequest(new { message = "An enrolment with a result cannot be cancelled." });

            _context.Enrolments.Remove(enrolment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/participants/results
        [HttpGet("results")]
        public async Task<IActionResult> GetMyResults()
        {
            int participantId = GetParticipantId();

            var results = await _context.Results
                .AsNoTracking()
                .Where(r => r.Enrolment.ParticipantID == participantId)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e.Event)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e.Category)
                .ToListAsync();

            return Ok(results);
        }

        // GET: api/participants/results/{resultId}
        [HttpGet("results/{resultId:int}")]
        public async Task<IActionResult> GetMyResult(int resultId)
        {
            int participantId = GetParticipantId();

            var result = await _context.Results
                .AsNoTracking()
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e.Event)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e.Category)
                .FirstOrDefaultAsync(r =>
                    r.ResultID == resultId &&
                    r.Enrolment.ParticipantID == participantId);

            if (result == null)
                return NotFound(new { message = "Result not found." });

            return Ok(result);
        }

        // GET: api/participants/events/{eventId}/result
        [HttpGet("events/{eventId:int}/result")]
        public async Task<IActionResult> GetMyEventResult(int eventId)
        {
            int participantId = GetParticipantId();

            var result = await _context.Results
                .AsNoTracking()
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e.Event)
                .Where(r => r.Enrolment.ParticipantID == participantId &&
                            r.Enrolment.EventID == eventId)
                .Select(r => new
                {
                    r.ResultID,
                    r.EnrolmentID,
                    r.FinishTime,
                    r.Position,
                    EventName = r.Enrolment.Event.EventName,
                    EventDate = r.Enrolment.Event.EventDate
                })
                .FirstOrDefaultAsync();

            if (result == null)
                return NotFound(new { message = "No result found for this event." });

            return Ok(result);
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
