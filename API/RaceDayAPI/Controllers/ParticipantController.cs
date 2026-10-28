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
    [Authorize(Roles = "Participant")]
    public class ParticipantController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ParticipantController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetParticipantId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int participantId) ? participantId : 0;
        }

        // GET: api/participant/profile
        /// <summary>
        /// Participant only. Views the logged-in participant's own profile.
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
        public async Task<ActionResult<Participant>> GetProfile()
        {
            var participant = await _context.Participants
                .FindAsync(GetParticipantId());

            if (participant == null)
            {
                return NotFound();
            }

            return Ok(new ProfileResponse
            {
                Id = participant.ParticipantID,
                FirstName = participant.FirstName,
                LastName = participant.LastName,
                Email = participant.Email
            });
        }

        // PUT: api/participant/profile
        /// <summary>
        /// Participant only. Updates the logged-in participant's first name, last name and email.
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
            var existingParticipant = await _context.Participants
                .FindAsync(GetParticipantId());

            if (existingParticipant == null)
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
                await _context.Participants.AnyAsync(p =>
                    p.Email == email &&
                    p.ParticipantID != existingParticipant.ParticipantID) ||
                await _context.Organizers.AnyAsync(o => o.Email == email);

            if (emailInUse)
            {
                return BadRequest("This email address is already in use.");
            }

            existingParticipant.FirstName = request.FirstName.Trim();
            existingParticipant.LastName = request.LastName.Trim();
            existingParticipant.Email = email;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/participant/enrolments
        /// <summary>
        /// Participant only. Lists the logged-in participant's enrolments with event, category and result.
        /// </summary>
        /// <returns>A list of enrolments.</returns>
        /// <response code="200">Enrolments returned.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [HttpGet("enrolments")]
        public async Task<IActionResult> GetEnrolments()
        {
            var enrolments = await _context.Enrolments
                .Where(e => e.ParticipantID == GetParticipantId())
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Result)
                .ToListAsync();

            return Ok(enrolments);
        }

        // GET: api/participant/results
        /// <summary>
        /// Participant only. Lists the logged-in participant's own race results.
        /// </summary>
        /// <returns>A list of results with finish time and position.</returns>
        /// <response code="200">Results returned.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [HttpGet("results")]
        public async Task<IActionResult> GetResults()
        {
            var results = await _context.Results
                .Include(r => r.Enrolment)
                .ThenInclude(e => e.Event)
                .Where(r => r.Enrolment.ParticipantID == GetParticipantId())
                .ToListAsync();

            return Ok(results);
        }
    }
}