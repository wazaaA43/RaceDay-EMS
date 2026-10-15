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
    [Authorize]
    public class EnrolmentController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int userId) ? userId : 0;
        }

        // GET: api/enrolment
        /// <summary>
        /// Organiser only. Lists all enrolments for the events that the logged-in organiser created.
        /// </summary>
        /// <returns>A list of enrolments with participant, event, category and result.</returns>
        /// <response code="200">Enrolments returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Organiser")]
        [HttpGet]
        public async Task<IActionResult> GetEnrolments()
        {
            var enrolments = await _context.Enrolments
                .Include(e => e.Participant)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .Where(e => e.Event.OrganizerID == GetUserId())
                .ToListAsync();

            return Ok(enrolments);
        }

        // GET: api/enrolment/my
        /// <summary>
        /// Participant only. Lists the logged-in participant's own enrolments.
        /// </summary>
        /// <returns>A list of the participant's enrolments.</returns>
        /// <response code="200">Enrolments returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Participant")]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var enrolments = await _context.Enrolments
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .Where(e => e.ParticipantID == GetUserId())
                .ToListAsync();

            return Ok(enrolments);
        }

        // GET: api/enrolment/1
        /// <summary>
        /// Gets one enrolment. A participant can only view their own; an organiser can only view enrolments for their own events.
        /// </summary>
        /// <param name="id">The enrolment ID.</param>
        /// <returns>The matching enrolment.</returns>
        /// <response code="200">Enrolment returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Enrolment not found.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEnrolment(int id)
        {
            var enrolment = await _context.Enrolments
                .Include(e => e.Participant)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .FirstOrDefaultAsync(e => e.EnrolmentID == id);

            if (enrolment == null)
            {
                return NotFound();
            }

            bool isParticipant =
                User.IsInRole("Participant") &&
                enrolment.ParticipantID == GetUserId();

            bool isOrganizer =
                User.IsInRole("Organiser") &&
                enrolment.Event.OrganizerID == GetUserId();

            if (!isParticipant && !isOrganizer)
            {
                return Forbid();
            }

            return Ok(enrolment);
        }

        // POST: api/enrolment
        /// <summary>
        /// Participant only. Enters an event by selecting one of its categories. The link between participant, event and category is recorded; the participant is taken from the token.
        /// </summary>
        /// <param name="enrolment">The EventID and the CategoryID being entered.</param>
        /// <returns>The newly created enrolment.</returns>
        /// <response code="201">Enrolment recorded.</response>
        /// <response code="400">Event missing or passed, category not part of the event, or already enrolled.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Participant")]
        [HttpPost]
        public async Task<ActionResult<Enrolment>> CreateEnrolment(Enrolment enrolment)
        {
            var eventExists = await _context.Events
                .AnyAsync(e => e.EventID == enrolment.EventID &&
                               e.EventDate >= DateTime.Today);

            if (!eventExists)
            {
                return BadRequest("The event does not exist or has already passed.");
            }

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.CategoryID == enrolment.CategoryID &&
                               c.EventID == enrolment.EventID);

            if (!categoryExists)
            {
                return BadRequest("The category does not belong to this event.");
            }

            int participantId = GetUserId();

            var duplicate = await _context.Enrolments.AnyAsync(e =>
                e.ParticipantID == participantId &&
                e.EventID == enrolment.EventID);

            if (duplicate)
            {
                return BadRequest("You are already enrolled in this event.");
            }

            enrolment.ParticipantID = participantId;

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEnrolment),
                new { id = enrolment.EnrolmentID },
                enrolment);
        }

        // DELETE: api/enrolment/1
        /// <summary>
        /// Participant only. Cancels the participant's own enrolment, unless a result has already been recorded for it.
        /// </summary>
        /// <param name="id">The enrolment ID.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">A result is already recorded.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Enrolment not found for this participant.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Participant")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelEnrolment(int id)
        {
            var enrolment = await _context.Enrolments
                .FirstOrDefaultAsync(e =>
                    e.EnrolmentID == id &&
                    e.ParticipantID == GetUserId());

            if (enrolment == null)
            {
                return NotFound();
            }

            if (await _context.Results.AnyAsync(r => r.EnrolmentID == id))
            {
                return BadRequest("An enrolment with a recorded result cannot be cancelled.");
            }

            _context.Enrolments.Remove(enrolment);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}