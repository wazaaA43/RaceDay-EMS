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
    [Authorize(Roles = "Organiser")]
    public class OrganizerController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public OrganizerController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetOrganizerId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int organizerId) ? organizerId : 0;
        }

        // GET: api/organizer/profile
        /// <summary>
        /// Organiser only. Views the logged-in organiser's own profile.
        /// </summary>
        /// <returns>Id, first name, last name and email (never the password).</returns>
        /// <response code="200">Profile returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Profile not found.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("profile")]
        public async Task<ActionResult<Organizer>> GetProfile()
        {
            var organizer = await _context.Organizers
                .FindAsync(GetOrganizerId());

            if (organizer == null)
            {
                return NotFound();
            }

            return Ok(new ProfileResponse
            {
                Id = organizer.OrganizerID,
                FirstName = organizer.FirstName,
                LastName = organizer.LastName,
                Email = organizer.Email
            });
        }

        // PUT: api/organizer/profile
        /// <summary>
        /// Organiser only. Updates the logged-in organiser's first name, last name and email.
        /// </summary>
        /// <param name="organizer">The new first name, last name and email.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Profile not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(Organizer organizer)
        {
            var existingOrganizer = await _context.Organizers
                .FindAsync(GetOrganizerId());

            if (existingOrganizer == null)
            {
                return NotFound();
            }

            existingOrganizer.FirstName = organizer.FirstName;
            existingOrganizer.LastName = organizer.LastName;
            existingOrganizer.Email = organizer.Email;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/organizer/events
        /// <summary>
        /// Organiser only. Lists the events created by the logged-in organiser.
        /// </summary>
        /// <returns>A list of the organiser's events.</returns>
        /// <response code="200">Events returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [HttpGet("events")]
        public async Task<IActionResult> GetMyEvents()
        {
            var events = await _context.Events
                .Where(e => e.OrganizerID == GetOrganizerId())
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/organizer/events/1
        /// <summary>
        /// Organiser only. Gets one event, provided it belongs to the logged-in organiser.
        /// </summary>
        /// <param name="id">The event ID.</param>
        /// <returns>The matching event.</returns>
        /// <response code="200">Event returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Event not found for this organiser.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("events/{id}")]
        public async Task<IActionResult> GetMyEvent(int id)
        {
            var item = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.EventID == id &&
                    e.OrganizerID == GetOrganizerId());

            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }
    }
}