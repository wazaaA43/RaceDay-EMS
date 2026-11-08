using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Services;
using System.Security.Claims;

namespace RaceDayAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetOrganizerId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int organizerId) ? organizerId : 0;
        }

        // GET: api/event
        /// <summary>
        /// Lists all events, soonest first. Public.
        /// </summary>
        /// <returns>A list of events.</returns>
        /// <response code="200">Events returned.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
        {
            return await _context.Events
                .OrderBy(e => e.EventDate)
                .ToListAsync();
        }

        // GET: api/event/1
        /// <summary>
        /// Gets a single event by ID. Public.
        /// </summary>
        /// <param name="id">The event ID.</param>
        /// <returns>The matching event.</returns>
        /// <response code="200">Event returned.</response>
        /// <response code="404">No event with that ID.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            var item = await _context.Events.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return item;
        }

        // GET: api/event/upcoming
        /// <summary>
        /// Lists events happening today or later. Public.
        /// </summary>
        /// <returns>A list of upcoming events.</returns>
        /// <response code="200">Events returned.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcomingEvents()
        {
            var events = await _context.Events
                .Where(e => e.EventDate >= DateTime.Today)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/event/past
        /// <summary>
        /// Lists events that have already taken place, most recent first. Public.
        /// </summary>
        /// <returns>A list of past events.</returns>
        /// <response code="200">Events returned.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [HttpGet("past")]
        public async Task<IActionResult> GetPastEvents()
        {
            var events = await _context.Events
                .Where(e => e.EventDate < DateTime.Today)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // POST: api/event
        /// <summary>
        /// Organiser only. Creates an event owned by the logged-in organiser.
        /// </summary>
        /// <param name="request">Event name, description, date, location, distance and event type (Run, Walk or Cycle).</param>
        /// <returns>The newly created event.</returns>
        /// <response code="201">Event created.</response>
        /// <response code="400">Missing details, distance not above zero, or invalid event type.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Organiser")]
        [HttpPost]
        public async Task<ActionResult<Event>> CreateEvent(EventRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EventName) ||
                string.IsNullOrWhiteSpace(request.Description) ||
                string.IsNullOrWhiteSpace(request.Location) ||
                request.Distance <= 0)
            {
                return BadRequest("Valid event details are required.");
            }

            if (!IsValidEventType(request.EventType))
            {
                return BadRequest("Event type must be Run, Walk or Cycle.");
            }

            var item = new Event
            {
                EventName = request.EventName.Trim(),
                Description = request.Description.Trim(),
                EventDate = request.EventDate,
                Location = request.Location.Trim(),
                Distance = request.Distance,
                EventType = NormaliseEventType(request.EventType),
                OrganizerID = GetOrganizerId()
            };

            _context.Events.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEvent),
                new { id = item.EventID },
                item);
        }

        // PUT: api/event/1
        /// <summary>
        /// Organiser only. Updates one of the organiser's own events.
        /// </summary>
        /// <param name="id">The event ID in the URL.</param>
        /// <param name="request">The updated event name, description, date, location, distance and event type (Run, Walk or Cycle).</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">Missing details or invalid event type.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Not an Organiser, or the event belongs to another organiser.</response>
        /// <response code="404">Event not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(int id, EventRequest request)
        {
            var existingEvent = await _context.Events.FindAsync(id);

            if (existingEvent == null)
            {
                return NotFound();
            }

            if (existingEvent.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.EventName) ||
                string.IsNullOrWhiteSpace(request.Description) ||
                string.IsNullOrWhiteSpace(request.Location) ||
                request.Distance <= 0)
            {
                return BadRequest("Valid event details are required.");
            }

            if (!IsValidEventType(request.EventType))
            {
                return BadRequest("Event type must be Run, Walk or Cycle.");
            }

            existingEvent.EventName = request.EventName.Trim();
            existingEvent.Description = request.Description.Trim();
            existingEvent.EventDate = request.EventDate;
            existingEvent.Location = request.Location.Trim();
            existingEvent.Distance = request.Distance;
            existingEvent.EventType = NormaliseEventType(request.EventType);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/event/1/banner
        /// <summary>
        /// Organiser only. Uploads a banner image (JPG, PNG or WEBP, up to 2 MB) for one of the organiser's own events. The image is stored in Azure Blob Storage.
        /// </summary>
        /// <param name="id">The event ID.</param>
        /// <param name="file">The image file.</param>
        /// <returns>The public URL of the stored banner.</returns>
        /// <response code="200">Banner stored; its URL is returned.</response>
        /// <response code="400">No file, wrong file type or file too large.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Not an Organiser, or the event belongs to another organiser.</response>
        /// <response code="404">Event not found.</response>
        /// <response code="503">Image storage is not available.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Authorize(Roles = "Organiser")]
        [HttpPost("{id}/banner")]
        public async Task<IActionResult> UploadBanner(int id, IFormFile file, [FromServices] IImageStorage storage)
        {
            var existingEvent = await _context.Events.FindAsync(id);

            if (existingEvent == null)
            {
                return NotFound();
            }

            if (existingEvent.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            var error = await ImageValidator.ValidateAsync(file);
            if (error != null)
            {
                return BadRequest(error);
            }

            try
            {
                existingEvent.BannerUrl = await storage.SaveAsync(file, "banners");
            }
            catch (Exception)
            {
                // Storage not configured or Azure unreachable: no raw error details are returned
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Image storage is not available right now. Please try again later.");
            }

            await _context.SaveChangesAsync();

            return Ok(new { bannerUrl = existingEvent.BannerUrl });
        }

        // DELETE: api/event/1
        /// <summary>
        /// Organiser only. Deletes one of the organiser's own events, provided it has no enrolments or categories.
        /// </summary>
        /// <param name="id">The event ID.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">The event still has enrolments or categories.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Event not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var item = await _context.Events.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            if (item.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (await _context.Enrolments.AnyAsync(e => e.EventID == id))
            {
                return BadRequest("The event has enrolments and cannot be deleted.");
            }

            if (await _context.Categories.AnyAsync(c => c.EventID == id))
            {
                return BadRequest("Delete the event categories first.");
            }

            _context.Events.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static bool IsValidEventType(string? value)
        {
            return value != null &&
                (value.Equals("Run", StringComparison.OrdinalIgnoreCase) ||
                 value.Equals("Walk", StringComparison.OrdinalIgnoreCase) ||
                 value.Equals("Cycle", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormaliseEventType(string value)
        {
            if (value.Equals("Run", StringComparison.OrdinalIgnoreCase))
                return "Run";

            if (value.Equals("Walk", StringComparison.OrdinalIgnoreCase))
                return "Walk";

            return "Cycle";
        }
    }
}