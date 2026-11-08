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
    // Requirement: event enrolment tests - Participants can enrol and the enrolment is correctly recorded
    public class EnrolmentFlowTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static EnrolmentController CreateController(RaceDayDbContext context, int participantId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, participantId.ToString()),
                new Claim(ClaimTypes.Role, "Participant")
            }, "TestAuth");

            return new EnrolmentController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private static async Task<(RaceDayAPI.Models.Event RaceEvent, Category RaceCategory)> SeedEventWithCategory(
            RaceDayDbContext context, DateTime eventDate)
        {
            var raceEvent = new RaceDayAPI.Models.Event
            {
                EventName = "Pretoria Fun Run",
                Description = "A 10 km fun run",
                EventDate = eventDate,
                Location = "Pretoria",
                Distance = 10,
                EventType = "Run",
                OrganizerID = 1
            };
            context.Events.Add(raceEvent);
            await context.SaveChangesAsync();

            var raceCategory = new Category
            {
                CategoryName = "Senior 10km",
                CategoryType = "Distance",
                EventID = raceEvent.EventID
            };
            context.Categories.Add(raceCategory);
            await context.SaveChangesAsync();

            return (raceEvent, raceCategory);
        }

        // Success: the participant, event and chosen category are all recorded on the enrolment
        [Fact]
        public async Task CreateEnrolment_ValidRequest_RecordsParticipantEventAndCategory()
        {
            using var context = CreateContext();
            var (raceEvent, raceCategory) = await SeedEventWithCategory(context, DateTime.Today.AddDays(10));
            var controller = CreateController(context, 5);

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = raceEvent.EventID,
                CategoryID = raceCategory.CategoryID
            });

            Assert.IsType<CreatedAtActionResult>(result.Result);
            var saved = Assert.Single(context.Enrolments);
            Assert.Equal(5, saved.ParticipantID);
            Assert.Equal(raceEvent.EventID, saved.EventID);
            Assert.Equal(raceCategory.CategoryID, saved.CategoryID);
        }

        // Failure: the category must belong to the event being entered
        [Fact]
        public async Task CreateEnrolment_CategoryFromAnotherEvent_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var (firstEvent, _) = await SeedEventWithCategory(context, DateTime.Today.AddDays(10));
            var (_, otherCategory) = await SeedEventWithCategory(context, DateTime.Today.AddDays(20));
            var controller = CreateController(context, 5);

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = firstEvent.EventID,
                CategoryID = otherCategory.CategoryID
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(context.Enrolments);
        }

        // Failure: a participant cannot enrol in the same event twice
        [Fact]
        public async Task CreateEnrolment_AlreadyEnrolled_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var (raceEvent, raceCategory) = await SeedEventWithCategory(context, DateTime.Today.AddDays(10));
            var controller = CreateController(context, 5);

            await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = raceEvent.EventID,
                CategoryID = raceCategory.CategoryID
            });
            var second = await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = raceEvent.EventID,
                CategoryID = raceCategory.CategoryID
            });

            Assert.IsType<BadRequestObjectResult>(second.Result);
            Assert.Single(context.Enrolments);
        }

        // Failure: enrolling in an event that has already passed is rejected
        [Fact]
        public async Task CreateEnrolment_EventAlreadyPassed_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var (raceEvent, raceCategory) = await SeedEventWithCategory(context, DateTime.Today.AddDays(-3));
            var controller = CreateController(context, 5);

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = raceEvent.EventID,
                CategoryID = raceCategory.CategoryID
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(context.Enrolments);
        }

        // Success: a participant can cancel their own enrolment
        [Fact]
        public async Task CancelEnrolment_OwnEnrolment_RemovesIt()
        {
            using var context = CreateContext();
            var (raceEvent, raceCategory) = await SeedEventWithCategory(context, DateTime.Today.AddDays(10));
            var controller = CreateController(context, 5);
            await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = raceEvent.EventID,
                CategoryID = raceCategory.CategoryID
            });
            int enrolmentId = context.Enrolments.Single().EnrolmentID;

            var result = await controller.CancelEnrolment(enrolmentId);

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(context.Enrolments);
        }

        // Failure: a participant cannot cancel someone else's enrolment
        [Fact]
        public async Task CancelEnrolment_OtherParticipantsEnrolment_ReturnsNotFound()
        {
            using var context = CreateContext();
            var (raceEvent, raceCategory) = await SeedEventWithCategory(context, DateTime.Today.AddDays(10));
            await CreateController(context, 5).CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = raceEvent.EventID,
                CategoryID = raceCategory.CategoryID
            });
            int enrolmentId = context.Enrolments.Single().EnrolmentID;

            var result = await CreateController(context, 6).CancelEnrolment(enrolmentId);

            Assert.IsType<NotFoundResult>(result);
            Assert.Single(context.Enrolments);
        }
    }
}