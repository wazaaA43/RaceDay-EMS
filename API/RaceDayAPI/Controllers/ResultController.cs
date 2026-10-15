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
    public class ResultController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetOrganizerId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int organizerId) ? organizerId : 0;
        }

        private int GetParticipantId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int participantId) ? participantId : 0;
        }

        private int GetUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int userId) ? userId : 0;
        }

        // GET: api/result
        /// <summary>
        /// Organiser only. Lists the results captured for the organiser's own events.
        /// </summary>
        /// <returns>A list of results.</returns>
        /// <response code="200">Results returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Organiser")]
        [HttpGet]
        public async Task<IActionResult> GetResults()
        {
            var results = await _context.Results
                .Include(r => r.Enrolment)
                .ThenInclude(e => e.Event)
                .Where(r => r.Enrolment.Event.OrganizerID == GetOrganizerId())
                .ToListAsync();

            return Ok(results);
        }

        // GET: api/result/1
        /// <summary>
        /// Gets one result. A participant can only view their own; an organiser can only view results for their own events.
        /// </summary>
        /// <param name="id">The result ID.</param>
        /// <returns>The matching result.</returns>
        /// <response code="200">Result returned.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Result not found.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetResult(int id)
        {
            var result = await _context.Results
                .Include(r => r.Enrolment)
                .ThenInclude(e => e.Event)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (result == null)
            {
                return NotFound();
            }

            bool isOrganizer =
                User.IsInRole("Organiser") &&
                result.Enrolment.Event.OrganizerID == GetOrganizerId();

            bool isParticipant =
                User.IsInRole("Participant") &&
                result.Enrolment.ParticipantID == GetParticipantId();

            if (!isOrganizer && !isParticipant)
            {
                return Forbid();
            }

            return Ok(result);
        }

        // POST: api/result
        /// <summary>
        /// Organiser only. Captures a finish time and finishing position for an enrolment on the organiser's own event.
        /// </summary>
        /// <param name="result">The EnrolmentID, the FinishTime (format hh:mm:ss) and the finishing Position.</param>
        /// <returns>The newly created result.</returns>
        /// <response code="201">Result captured.</response>
        /// <response code="400">Invalid time or position, or a result already exists for this enrolment.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Not an Organiser, or the event belongs to another organiser.</response>
        /// <response code="404">Enrolment not found.</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpPost]
        public async Task<ActionResult<Result>> CreateResult(Result result)
        {
            if (result.FinishTime <= TimeSpan.Zero || result.Position <= 0)
            {
                return BadRequest("A valid finish time and position are required.");
            }

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentID == result.EnrolmentID);

            if (enrolment == null)
            {
                return NotFound("Enrolment not found.");
            }

            if (enrolment.Event.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (await _context.Results.AnyAsync(r =>
                r.EnrolmentID == result.EnrolmentID))
            {
                return BadRequest("A result already exists for this enrolment.");
            }

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetResult),
                new { id = result.ResultID },
                result);
        }

        // PUT: api/result/1
        /// <summary>
        /// Organiser only. Corrects the finish time and position of a result on the organiser's own event.
        /// </summary>
        /// <param name="id">The result ID in the URL.</param>
        /// <param name="result">The updated finish time (hh:mm:ss) and position; its ResultID must match the URL ID.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">ID mismatch or invalid time/position.</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Result not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateResult(int id, Result result)
        {
            if (id != result.ResultID)
            {
                return BadRequest("The ID in the URL does not match the result ID.");
            }

            var existingResult = await _context.Results
                .Include(r => r.Enrolment)
                .ThenInclude(e => e.Event)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (existingResult == null)
            {
                return NotFound();
            }

            if (existingResult.Enrolment.Event.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (result.FinishTime <= TimeSpan.Zero || result.Position <= 0)
            {
                return BadRequest("A valid finish time and position are required.");
            }

            existingResult.FinishTime = result.FinishTime;
            existingResult.Position = result.Position;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/result/1
        /// <summary>
        /// Organiser only. Deletes a result on the organiser's own event.
        /// </summary>
        /// <param name="id">The result ID.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="401">No valid token supplied: log in and authorise first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Result not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResult(int id)
        {
            var result = await _context.Results
                .Include(r => r.Enrolment)
                .ThenInclude(e => e.Event)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (result == null)
            {
                return NotFound();
            }

            if (result.Enrolment.Event.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            _context.Results.Remove(result);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}