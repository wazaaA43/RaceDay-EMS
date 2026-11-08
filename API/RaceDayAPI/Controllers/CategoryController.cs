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
    public class CategoryController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoryController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int GetOrganizerId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int organizerId) ? organizerId : 0;
        }

        // GET: api/category
        /// <summary>
        /// Lists every category across all events. Public.
        /// </summary>
        /// <returns>A list of categories with their event.</returns>
        /// <response code="200">Categories returned.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            return await _context.Categories
                .Include(c => c.Event)
                .ToListAsync();
        }

        // GET: api/category/1
        /// <summary>
        /// Gets a single category by its ID. Public.
        /// </summary>
        /// <param name="id">The category ID.</param>
        /// <returns>The matching category.</returns>
        /// <response code="200">Category returned.</response>
        /// <response code="404">No category with that ID.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(int id)
        {
            var category = await _context.Categories
                .FindAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            return category;
        }

        // GET: api/category/event/1
        /// <summary>
        /// Lists the age or distance categories defined for one event. Public.
        /// </summary>
        /// <param name="eventId">The event ID.</param>
        /// <returns>A list of categories for that event.</returns>
        /// <response code="200">Categories returned.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [HttpGet("event/{eventId}")]
        public async Task<IActionResult> GetEventCategories(int eventId)
        {
            var categories = await _context.Categories
                .Where(c => c.EventID == eventId)
                .ToListAsync();

            return Ok(categories);
        }

        // POST: api/category
        /// <summary>
        /// Organiser only. Defines a new age or distance category (for example Under 20, Senior, 10km) for one of the organiser's own events.
        /// </summary>
        /// <param name="request">Category name, category type and the EventID it belongs to.</param>
        /// <returns>The newly created category.</returns>
        /// <response code="201">Category created.</response>
        /// <response code="400">Category name or type missing.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Not an Organiser, or the event belongs to another organiser.</response>
        /// <response code="404">Event not found.</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpPost]
        public async Task<ActionResult<Category>> CreateCategory(CreateCategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CategoryName) ||
                string.IsNullOrWhiteSpace(request.CategoryType))
            {
                return BadRequest("Category name and type are required.");
            }

            var item = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == request.EventID);

            if (item == null)
            {
                return NotFound("Event not found.");
            }

            if (item.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            var category = new Category
            {
                CategoryName = request.CategoryName.Trim(),
                CategoryType = request.CategoryType.Trim(),
                EventID = request.EventID
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetCategory),
                new { id = category.CategoryID },
                category);
        }

        // PUT: api/category/1
        /// <summary>
        /// Organiser only. Updates the name and type of a category on the organiser's own event.
        /// </summary>
        /// <param name="id">The category ID in the URL.</param>
        /// <param name="request">The updated category name and type.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">Name or type missing.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Category not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, UpdateCategoryRequest request)
        {
            var existingCategory = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (existingCategory == null)
            {
                return NotFound();
            }

            if (existingCategory.Event.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.CategoryName) ||
                string.IsNullOrWhiteSpace(request.CategoryType))
            {
                return BadRequest("Category name and type are required.");
            }

            existingCategory.CategoryName = request.CategoryName.Trim();
            existingCategory.CategoryType = request.CategoryType.Trim();

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/category/1
        /// <summary>
        /// Organiser only. Deletes a category on the organiser's own event, provided nobody has enrolled in it.
        /// </summary>
        /// <param name="id">The category ID.</param>
        /// <returns>No content when successful.</returns>
        /// <response code="204">Updated or deleted successfully (no content returned).</response>
        /// <response code="400">The category already has enrolments.</response>
        /// <response code="401">Not logged in: log in first.</response>
        /// <response code="403">Logged in, but the role or ownership does not allow this action.</response>
        /// <response code="404">Category not found.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound();
            }

            if (category.Event.OrganizerID != GetOrganizerId())
            {
                return Forbid();
            }

            if (await _context.Enrolments.AnyAsync(e => e.CategoryID == id))
            {
                return BadRequest("The category has enrolments and cannot be deleted.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}