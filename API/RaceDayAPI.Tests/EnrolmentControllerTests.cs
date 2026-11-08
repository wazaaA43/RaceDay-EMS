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
    public class EnrolmentControllerTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static EnrolmentController CreateController(
            RaceDayDbContext context, int participantId)
        {
            var controller = new EnrolmentController(context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, participantId.ToString()),
                            new Claim(ClaimTypes.Role, "Participant")
                        }, "Test"))
                }
            };

            return controller;
        }

        private static async Task SeedParticipantEventAndCategory(
            RaceDayDbContext context, DateTime eventDate)
        {
            context.Participants.Add(new Participant
            {
                ParticipantID = 1,
                FirstName = "Test",
                LastName = "Participant",
                Email = "participant@example.com",
                PasswordHash = "test-hash"
            });

            context.Events.Add(new RaceDayAPI.Models.Event
            {
                EventID = 1,
                EventName = "Test Run",
                Description = "Test event",
                EventDate = eventDate,
                Location = "Pretoria",
                Distance = 5,
                EventType = "Run",
                OrganizerID = 10
            });

            context.Categories.Add(new Category
            {
                CategoryID = 1,
                CategoryName = "Open",
                CategoryType = "Age",
                EventID = 1
            });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateEnrolment_ValidRequest_ReturnsCreated()
        {
            using var context = CreateContext();
            await SeedParticipantEventAndCategory(
                context, DateTime.Today.AddDays(7));

            var controller = CreateController(context, 1);

            var enrolment = new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 1
            };

            var result = await controller.CreateEnrolment(enrolment);

            Assert.IsType<CreatedAtActionResult>(result.Result);

            var saved = await context.Enrolments.SingleAsync();
            Assert.Equal(1, saved.ParticipantID);
            Assert.Equal(1, saved.EventID);
            Assert.Equal(1, saved.CategoryID);
        }

        [Fact]
        public async Task CreateEnrolment_DuplicateEvent_ReturnsBadRequest()
        {
            using var context = CreateContext();
            await SeedParticipantEventAndCategory(
                context, DateTime.Today.AddDays(7));

            context.Enrolments.Add(new Enrolment
            {
                ParticipantID = 1,
                EventID = 1,
                CategoryID = 1
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, 1);

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 1
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateEnrolment_EventAlreadyPassed_ReturnsBadRequest()
        {
            using var context = CreateContext();
            await SeedParticipantEventAndCategory(
                context, DateTime.Today.AddDays(-2));

            var controller = CreateController(context, 1);

            var result = await controller.CreateEnrolment(new CreateEnrolmentRequest
            {
                EventID = 1,
                CategoryID = 1
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(await context.Enrolments.ToListAsync());
        }
    }
}