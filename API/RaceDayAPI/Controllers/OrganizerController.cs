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
    [Route("api/organizers")]
    [Authorize(Roles = "Organiser")]
    public class OrganizerController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly PasswordHasher<object> _passwordHasher = new();

        public OrganizerController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetOrganizerId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(id, out int organizerId)
                ? organizerId
                : 0;
        }

        // GET: api/organizers/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            int organizerId = GetOrganizerId();

            var organizer = await _context.Organizers
                .AsNoTracking()
                .Where(o => o.OrganizerID == organizerId)
                .Select(o => new
                {
                    o.OrganizerID,
                    o.FirstName,
                    o.LastName,
                    o.Email
                })
                .FirstOrDefaultAsync();

            if (organizer == null)
                return NotFound(new { message = "Organizer profile not found." });

            return Ok(organizer);
        }

        // PUT: api/organizers/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(
            UpdateOrganizerProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new
                {
                    message = "First name, last name and email are required."
                });
            }

            int organizerId = GetOrganizerId();

            var organizer = await _context.Organizers.FindAsync(organizerId);

            if (organizer == null)
                return NotFound(new { message = "Organizer not found." });

            string email = request.Email.Trim();

            bool organizerEmailExists = await _context.Organizers
                .AnyAsync(o => o.Email == email &&
                               o.OrganizerID != organizerId);

            bool participantEmailExists = await _context.Participants
                .AnyAsync(p => p.Email == email);

            if (organizerEmailExists || participantEmailExists)
                return BadRequest(new { message = "Email address is already in use." });

            organizer.FirstName = request.FirstName.Trim();
            organizer.LastName = request.LastName.Trim();
            organizer.Email = email;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully." });
        }

        // PUT: api/organizers/password
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword(
            ChangeOrganizerPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Both passwords are required." });
            }

            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "New password must be at least 8 characters." });

            var organizer = await _context.Organizers.FindAsync(GetOrganizerId());

            if (organizer == null)
                return NotFound(new { message = "Organizer not found." });

            var verification = _passwordHasher.VerifyHashedPassword(
                organizer,
                organizer.PasswordHash,
                request.CurrentPassword);

            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Current password is incorrect." });

            organizer.PasswordHash = _passwordHasher.HashPassword(
                organizer,
                request.NewPassword);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully." });
        }

        // GET: api/organizers/events
        [HttpGet("events")]
        public async Task<IActionResult> GetMyEvents()
        {
            int organizerId = GetOrganizerId();

            var events = await _context.Events
                .AsNoTracking()
                .Where(e => e.OrganizerID == organizerId)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // POST: api/events
        [HttpPost("/api/events")]
        public async Task<IActionResult> CreateEvent(CreateEventRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EventName) ||
                string.IsNullOrWhiteSpace(request.Description) ||
                string.IsNullOrWhiteSpace(request.Location) ||
                string.IsNullOrWhiteSpace(request.EventType))
            {
                return BadRequest(new { message = "All event fields are required." });
            }

            if (request.Distance <= 0)
                return BadRequest(new { message = "Distance must be greater than zero." });

            string eventType = request.EventType.Trim();

            if (!new[] { "Run", "Walk", "Cycle" }
                .Contains(eventType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Event type must be Run, Walk or Cycle." });
            }

            var eventItem = new RaceDayAPI.Models.Event
            {
                EventName = request.EventName.Trim(),
                Description = request.Description.Trim(),
                EventDate = request.EventDate,
                Location = request.Location.Trim(),
                Distance = request.Distance,
                EventType = eventType,
                OrganizerID = GetOrganizerId()
            };

            _context.Events.Add(eventItem);
            await _context.SaveChangesAsync();

            return Created($"/api/events/{eventItem.EventID}", eventItem);
        }

        // PUT: api/events/{eventId}
        [HttpPut("/api/events/{eventId:int}")]
        public async Task<IActionResult> UpdateEvent(
            int eventId,
            CreateEventRequest request)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganizerID != GetOrganizerId())
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.EventName) ||
                string.IsNullOrWhiteSpace(request.Description) ||
                string.IsNullOrWhiteSpace(request.Location) ||
                string.IsNullOrWhiteSpace(request.EventType) ||
                request.Distance <= 0)
            {
                return BadRequest(new { message = "Valid event details are required." });
            }

            string eventType = request.EventType.Trim();

            if (!new[] { "Run", "Walk", "Cycle" }
                .Contains(eventType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Event type must be Run, Walk or Cycle." });
            }

            eventItem.EventName = request.EventName.Trim();
            eventItem.Description = request.Description.Trim();
            eventItem.EventDate = request.EventDate;
            eventItem.Location = request.Location.Trim();
            eventItem.Distance = request.Distance;
            eventItem.EventType = eventType;

            await _context.SaveChangesAsync();

            return Ok(eventItem);
        }

        // DELETE: api/events/{eventId}
        [HttpDelete("/api/events/{eventId:int}")]
        public async Task<IActionResult> DeleteEvent(int eventId)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganizerID != GetOrganizerId())
                return Forbid();

            bool hasEnrolments = await _context.Enrolments
                .AnyAsync(e => e.EventID == eventId);

            if (hasEnrolments)
            {
                return BadRequest(new
                {
                    message = "This event has enrolments and cannot be deleted."
                });
            }

            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/events/{eventId}/categories
        [HttpPost("/api/events/{eventId:int}/categories")]
        public async Task<IActionResult> CreateCategory(
            int eventId,
            CategoryRequest request)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganizerID != GetOrganizerId())
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.CategoryName) ||
                string.IsNullOrWhiteSpace(request.CategoryType))
            {
                return BadRequest(new { message = "Category name and type are required." });
            }

            var category = new Category
            {
                CategoryName = request.CategoryName.Trim(),
                CategoryType = request.CategoryType.Trim(),
                EventID = eventId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Created($"/api/categories/{category.CategoryID}", category);
        }

        // PUT: api/categories/{categoryId}
        [HttpPut("/api/categories/{categoryId:int}")]
        public async Task<IActionResult> UpdateCategory(
            int categoryId,
            CategoryRequest request)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == categoryId);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event.OrganizerID != GetOrganizerId())
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.CategoryName) ||
                string.IsNullOrWhiteSpace(request.CategoryType))
            {
                return BadRequest(new { message = "Category name and type are required." });
            }

            category.CategoryName = request.CategoryName.Trim();
            category.CategoryType = request.CategoryType.Trim();

            await _context.SaveChangesAsync();

            return Ok(category);
        }

        // DELETE: api/categories/{categoryId}
        [HttpDelete("/api/categories/{categoryId:int}")]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == categoryId);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event.OrganizerID != GetOrganizerId())
                return Forbid();

            bool hasEnrolments = await _context.Enrolments
                .AnyAsync(e => e.CategoryID == categoryId);

            if (hasEnrolments)
            {
                return BadRequest(new
                {
                    message = "This category has enrolments and cannot be deleted."
                });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/events/{eventId}/enrolments
        [HttpGet("/api/events/{eventId:int}/enrolments")]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (eventItem == null)
                return NotFound(new { message = "Event not found." });

            if (eventItem.OrganizerID != GetOrganizerId())
                return Forbid();

            var enrolments = await _context.Enrolments
                .AsNoTracking()
                .Where(e => e.EventID == eventId)
                .Include(e => e.Participant)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .Select(e => new
                {
                    e.EnrolmentID,
                    e.EventID,
                    e.CategoryID,
                    Participant = new
                    {
                        e.Participant.ParticipantID,
                        e.Participant.FirstName,
                        e.Participant.LastName,
                        e.Participant.Email
                    },
                    Category = e.Category.CategoryName,
                    e.Result
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        // GET: api/enrolments/{enrolmentId}
        [HttpGet("/api/enrolments/{enrolmentId:int}")]
        public async Task<IActionResult> GetEnrolment(int enrolmentId)
        {
            var enrolment = await _context.Enrolments
                .AsNoTracking()
                .Include(e => e.Event)
                .Include(e => e.Participant)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.Event.OrganizerID != GetOrganizerId())
                return Forbid();

            return Ok(enrolment);
        }

        // POST: api/enrolments/{enrolmentId}/result
        [HttpPost("/api/enrolments/{enrolmentId:int}/result")]
        public async Task<IActionResult> RecordResult(
            int enrolmentId,
            RecordResultRequest request)
        {
            if (request.FinishTime < TimeSpan.Zero || request.Position < 1)
            {
                return BadRequest(new
                {
                    message = "Finish time must be non-negative and position must be at least 1."
                });
            }

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.Event.OrganizerID != GetOrganizerId())
                return Forbid();

            bool resultExists = await _context.Results
                .AnyAsync(r => r.EnrolmentID == enrolmentId);

            if (resultExists)
                return BadRequest(new { message = "A result already exists for this enrolment." });

            var result = new Result
            {
                EnrolmentID = enrolmentId,
                FinishTime = request.FinishTime,
                Position = request.Position
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return Created($"/api/participants/results/{result.ResultID}", result);
        }
    }

    public class UpdateOrganizerProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ChangeOrganizerPasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CreateEventRequest
    {
        public string EventName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal Distance { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    public class CategoryRequest
    {
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryType { get; set; } = string.Empty;
    }

    public class RecordResultRequest
    {
        public TimeSpan FinishTime { get; set; }
        public int Position { get; set; }
    }
}