using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using Xunit;

namespace RaceDayAPI.Tests
{
    // Publishing results, and a participant seeing only published results with the finisher count
    public class ResultPublishTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static ClaimsPrincipal CreateUser(int id, string role)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));
        }

        private static ResultController CreateResultController(RaceDayDbContext context, int organizerId)
        {
            return new ResultController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreateUser(organizerId, "Organiser") }
                }
            };
        }

        private static ParticipantController CreateParticipantController(RaceDayDbContext context, int participantId)
        {
            return new ParticipantController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreateUser(participantId, "Participant") }
                }
            };
        }

        // Creates an event, a category, and one enrolment with an unpublished result per participant (positions 1, 2, ...)
        private static async Task<RaceDayAPI.Models.Event> SeedEventWithResults(
            RaceDayDbContext context, int organizerId, DateTime eventDate, params int[] participantIds)
        {
            var raceEvent = new RaceDayAPI.Models.Event
            {
                EventName = "Pretoria Fun Run",
                Description = "A 10 km fun run",
                EventDate = eventDate,
                Location = "Pretoria",
                Distance = 10,
                EventType = "Run",
                OrganizerID = organizerId
            };
            context.Events.Add(raceEvent);
            await context.SaveChangesAsync();

            var category = new Category
            {
                CategoryName = "Senior",
                CategoryType = "Age",
                EventID = raceEvent.EventID
            };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            int position = 1;
            foreach (var participantId in participantIds)
            {
                var enrolment = new Enrolment
                {
                    ParticipantID = participantId,
                    EventID = raceEvent.EventID,
                    CategoryID = category.CategoryID
                };
                context.Enrolments.Add(enrolment);
                await context.SaveChangesAsync();

                context.Results.Add(new Result
                {
                    EnrolmentID = enrolment.EnrolmentID,
                    FinishTime = TimeSpan.FromMinutes(50 + position),
                    Position = position
                });
                await context.SaveChangesAsync();

                position++;
            }

            return raceEvent;
        }

        // Success: the owning organiser publishes every result for a finished event
        [Fact]
        public async Task PublishEventResults_ByOwningOrganiser_PublishesAllResults()
        {
            using var context = CreateContext();
            var raceEvent = await SeedEventWithResults(context, 1, DateTime.Today.AddDays(-2), 5, 6);
            var controller = CreateResultController(context, 1);

            var result = await controller.PublishEventResults(raceEvent.EventID);

            Assert.IsType<NoContentResult>(result);
            Assert.All(context.Results, r => Assert.True(r.IsPublished));
        }

        // Failure: another organiser cannot publish
        [Fact]
        public async Task PublishEventResults_ByDifferentOrganiser_ReturnsForbid()
        {
            using var context = CreateContext();
            var raceEvent = await SeedEventWithResults(context, 1, DateTime.Today.AddDays(-2), 5);
            var controller = CreateResultController(context, 2);

            var result = await controller.PublishEventResults(raceEvent.EventID);

            Assert.IsType<ForbidResult>(result);
            Assert.All(context.Results, r => Assert.False(r.IsPublished));
        }

        // Failure: results cannot be published before the event has taken place
        [Fact]
        public async Task PublishEventResults_EventNotYetHeld_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var raceEvent = await SeedEventWithResults(context, 1, DateTime.Today.AddDays(5), 5);
            var controller = CreateResultController(context, 1);

            var result = await controller.PublishEventResults(raceEvent.EventID);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.All(context.Results, r => Assert.False(r.IsPublished));
        }

        // Failure: nothing to publish
        [Fact]
        public async Task PublishEventResults_NoResults_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var raceEvent = await SeedEventWithResults(context, 1, DateTime.Today.AddDays(-2));
            var controller = CreateResultController(context, 1);

            var result = await controller.PublishEventResults(raceEvent.EventID);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Success: a participant sees only published results, with the number of finishers
        [Fact]
        public async Task GetResults_ReturnsOnlyPublishedResultsWithFinisherCount()
        {
            using var context = CreateContext();

            await SeedEventWithResults(context, 1, DateTime.Today.AddDays(-10), 5, 6);
            foreach (var published in context.Results)
            {
                published.IsPublished = true;
            }
            await context.SaveChangesAsync();

            // A second event whose result for participant 5 is still unpublished
            await SeedEventWithResults(context, 1, DateTime.Today.AddDays(-3), 5);

            var controller = CreateParticipantController(context, 5);

            var response = await controller.GetResults();

            var ok = Assert.IsType<OkObjectResult>(response);
            var list = Assert.IsType<List<ParticipantResultResponse>>(ok.Value);

            var item = Assert.Single(list);
            Assert.Equal(1, item.Position);
            Assert.Equal(2, item.TotalFinishers);
            Assert.Equal("Senior", item.CategoryName);
        }
    }
}