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
        /// <response code="401">Not logged in: log in first.</response>
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
        /// <param name="request">The new first name, last name and email.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated successfully (no content returned).</response>
        /// <response code="400">Missing details or the email address is already in use.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Profile not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(ProfileUpdateRequest request)
        {
            var existingOrganizer = await _context.Organizers
                .FindAsync(GetOrganizerId());

            if (existingOrganizer == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("First name, last name and email are required.");
            }

            string email = request.Email.Trim();

            bool emailInUse =
                await _context.Organizers.AnyAsync(o =>
                    o.Email == email &&
                    o.OrganizerID != existingOrganizer.OrganizerID) ||
                await _context.Participants.AnyAsync(p => p.Email == email);

            if (emailInUse)
            {
                return BadRequest("This email address is already in use.");
            }

            existingOrganizer.FirstName = request.FirstName.Trim();
            existingOrganizer.LastName = request.LastName.Trim();
            existingOrganizer.Email = email;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/organizer/events
        /// <summary>
        /// Organiser only. Lists the events created by the logged-in organiser.
        /// </summary>
        /// <returns>A list of the organiser's events.</returns>
        /// <response code="200">Events returned.</response>
        /// <response code="401">Not logged in: log in first.</response>
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
        /// <response code="401">Not logged in: log in first.</response>
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