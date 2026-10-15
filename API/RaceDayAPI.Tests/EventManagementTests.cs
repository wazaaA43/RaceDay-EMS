using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using Xunit;

namespace RaceDayAPI.Tests
{
    // Requirement: event management tests - Organisers can manage events (success and failure)
    public class EventManagementTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        // Builds the controller as if a logged-in user (with a role claim) is calling it
        private static EventController CreateController(RaceDayDbContext context, int userId, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth");

            return new EventController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private static RaceDayAPI.Models.Event NewEvent(int organizerId)
        {
            return new RaceDayAPI.Models.Event
            {
                EventName = "Pretoria Fun Run",
                Description = "A 10 km fun run",
                EventDate = DateTime.Today.AddDays(14),
                Location = "Pretoria",
                Distance = 10,
                EventType = "Run",
                OrganizerID = organizerId
            };
        }

        // Success: an organiser creates an event and it is saved against that organiser
        [Fact]
        public async Task CreateEvent_AsOrganiser_SavesEventForLoggedInOrganiser()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 7, "Organiser");
            var item = NewEvent(0);
            item.EventType = "run";

            var result = await controller.CreateEvent(item);

            Assert.IsType<CreatedAtActionResult>(result.Result);
            var saved = Assert.Single(context.Events);
            Assert.Equal(7, saved.OrganizerID);
            Assert.Equal("Run", saved.EventType);
        }

        // Failure: event type must be Run, Walk or Cycle
        [Fact]
        public async Task CreateEvent_InvalidEventType_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 1, "Organiser");
            var item = NewEvent(1);
            item.EventType = "Swim";

            var result = await controller.CreateEvent(item);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(context.Events);
        }

        // Success: the owning organiser can update their event
        [Fact]
        public async Task UpdateEvent_ByOwningOrganiser_UpdatesEvent()
        {
            using var context = CreateContext();
            var existing = NewEvent(1);
            context.Events.Add(existing);
            await context.SaveChangesAsync();

            var controller = CreateController(context, 1, "Organiser");
            var update = NewEvent(1);
            update.EventID = existing.EventID;
            update.EventName = "Updated Fun Run";

            var result = await controller.UpdateEvent(existing.EventID, update);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("Updated Fun Run", context.Events.Single().EventName);
        }

        // Failure: another organiser cannot update someone else's event
        [Fact]
        public async Task UpdateEvent_ByDifferentOrganiser_ReturnsForbid()
        {
            using var context = CreateContext();
            var existing = NewEvent(1);
            context.Events.Add(existing);
            await context.SaveChangesAsync();

            var controller = CreateController(context, 2, "Organiser");
            var update = NewEvent(1);
            update.EventID = existing.EventID;
            update.EventName = "Hijacked";

            var result = await controller.UpdateEvent(existing.EventID, update);

            Assert.IsType<ForbidResult>(result);
            Assert.Equal("Pretoria Fun Run", context.Events.Single().EventName);
        }

        // Success: the owning organiser can delete an event with no enrolments
        [Fact]
        public async Task DeleteEvent_ByOwningOrganiser_RemovesEvent()
        {
            using var context = CreateContext();
            var existing = NewEvent(1);
            context.Events.Add(existing);
            await context.SaveChangesAsync();

            var controller = CreateController(context, 1, "Organiser");

            var result = await controller.DeleteEvent(existing.EventID);

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(context.Events);
        }

        // Failure: another organiser cannot delete someone else's event
        [Fact]
        public async Task DeleteEvent_ByDifferentOrganiser_ReturnsForbid()
        {
            using var context = CreateContext();
            var existing = NewEvent(1);
            context.Events.Add(existing);
            await context.SaveChangesAsync();

            var controller = CreateController(context, 2, "Organiser");

            var result = await controller.DeleteEvent(existing.EventID);

            Assert.IsType<ForbidResult>(result);
            Assert.Single(context.Events);
        }

        // Failure: an event that already has enrolments cannot be deleted
        [Fact]
        public async Task DeleteEvent_WithEnrolments_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var existing = NewEvent(1);
            context.Events.Add(existing);
            await context.SaveChangesAsync();

            context.Enrolments.Add(new Enrolment
            {
                ParticipantID = 1,
                EventID = existing.EventID,
                CategoryID = 1
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, 1, "Organiser");

            var result = await controller.DeleteEvent(existing.EventID);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Single(context.Events);
        }
    }
}