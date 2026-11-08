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
                Email = participant.Email,
                ProfilePictureUrl = participant.ProfilePictureUrl
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

        // POST: api/participant/profile-picture
        /// <summary>
        /// Participant only. Uploads the logged-in participant's profile picture (JPG, PNG or WEBP, up to 2 MB). The image is stored in Azure Blob Storage.
        /// </summary>
        /// <param name="file">The image file.</param>
        /// <returns>The public URL of the stored picture.</returns>
        /// <response code="200">Picture stored; its URL is returned.</response>
        /// <response code="400">No file, wrong file type or file too large.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Profile not found.</response>
        /// <response code="503">Image storage is not available.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [HttpPost("profile-picture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file, [FromServices] IImageStorage storage)
        {
            var participant = await _context.Participants
                .FindAsync(GetParticipantId());

            if (participant == null)
            {
                return NotFound();
            }

            var error = await ImageValidator.ValidateAsync(file);
            if (error != null)
            {
                return BadRequest(error);
            }

            try
            {
                participant.ProfilePictureUrl = await storage.SaveAsync(file, "profiles");
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Image storage is not available right now. Please try again later.");
            }

            await _context.SaveChangesAsync();

            return Ok(new { profilePictureUrl = participant.ProfilePictureUrl });
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
            int participantId = GetParticipantId();

            // Only published results are shown; the finisher count is the number of published results for that event
            var results = await _context.Results
                .Where(r => r.IsPublished && r.Enrolment.ParticipantID == participantId)
                .Select(r => new ParticipantResultResponse
                {
                    ResultID = r.ResultID,
                    EventName = r.Enrolment.Event.EventName,
                    EventDate = r.Enrolment.Event.EventDate,
                    CategoryName = r.Enrolment.Category.CategoryName,
                    FinishTime = r.FinishTime,
                    Position = r.Position,
                    TotalFinishers = _context.Results.Count(x =>
                        x.IsPublished && x.Enrolment.EventID == r.Enrolment.EventID)
                })
                .ToListAsync();

            return Ok(results.OrderByDescending(r => r.EventDate).ToList());
        }
    }
}