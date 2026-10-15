using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
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
        /// <param name="item">Event name, description, date, location, distance and event type (Run, Walk or Cycle).</param>
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
        public async Task<ActionResult<Event>> CreateEvent(Event item)
        {
            if (string.IsNullOrWhiteSpace(item.EventName) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                string.IsNullOrWhiteSpace(item.Location) ||
                item.Distance <= 0)
            {
                return BadRequest("Valid event details are required.");
            }

            if (!IsValidEventType(item.EventType))
            {
                return BadRequest("Event type must be Run, Walk or Cycle.");
            }

            item.EventName = item.EventName.Trim();
            item.Description = item.Description.Trim();
            item.Location = item.Location.Trim();
            item.EventType = NormaliseEventType(item.EventType);
            item.OrganizerID = GetOrganizerId();

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
        /// <param name="item">The updated event details; its EventID must match the URL ID.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">ID mismatch, missing details or invalid event type.</response>
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
        public async Task<IActionResult> UpdateEvent(int id, Event item)
        {
            if (id != item.EventID)
            {
                return BadRequest("The ID in the URL does not match the event ID.");
            }

            var existingEvent = await _context.Events.FindAsync(id);

            if (existingEvent == null)
            {
                return NotFound();
            }

            if (existingEvent.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(item.EventName) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                string.IsNullOrWhiteSpace(item.Location) ||
                item.Distance <= 0)
            {
                return BadRequest("Valid event details are required.");
            }

            if (!IsValidEventType(item.EventType))
            {
                return BadRequest("Event type must be Run, Walk or Cycle.");
            }

            existingEvent.EventName = item.EventName.Trim();
            existingEvent.Description = item.Description.Trim();
            existingEvent.EventDate = item.EventDate;
            existingEvent.Location = item.Location.Trim();
            existingEvent.Distance = item.Distance;
            existingEvent.EventType = NormaliseEventType(item.EventType);

            await _context.SaveChangesAsync();

            return NoContent();
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