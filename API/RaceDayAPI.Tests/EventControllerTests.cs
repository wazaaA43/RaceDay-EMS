using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using Xunit;

namespace RaceDayAPI.Tests
{
    public class EventControllerTests
    {
        private RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        // Test 1: GET returns the events stored in the database
        [Fact]
        public async Task GetEvents_ReturnsAllEvents()
        {
            using var context = CreateContext();

            context.Events.Add(new RaceDayAPI.Models.Event
            {
                EventName = "Pretoria Fun Run",
                Description = "A 5 km fun run",
                EventDate = DateTime.Today.AddDays(7),
                Location = "Pretoria",
                Distance = 5,
                EventType = "Run",
                OrganizerID = 1
            });

            await context.SaveChangesAsync();

            var controller = new EventController(context);

            var result = await controller.GetEvents();

            Assert.NotNull(result.Value);
            Assert.Single(result.Value);
            Assert.Equal("Pretoria Fun Run", result.Value.First().EventName);
        }

        // Test 2: GET returns NotFound when the event does not exist
        [Fact]
        public async Task GetEvent_EventDoesNotExist_ReturnsNotFound()
        {
            using var context = CreateContext();

            var controller = new EventController(context);

            var result = await controller.GetEvent(999);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        // Test 3: POST rejects an event with invalid details
        [Fact]
        public async Task CreateEvent_InvalidDetails_ReturnsBadRequest()
        {
            using var context = CreateContext();

            var controller = new EventController(context);

            var item = new RaceDayAPI.Models.Event
            {
                EventName = "",
                Description = "Invalid event",
                EventDate = DateTime.Today.AddDays(7),
                Location = "Pretoria",
                Distance = 5,
                EventType = "Run",
                OrganizerID = 1
            };

            var result = await controller.CreateEvent(item);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
