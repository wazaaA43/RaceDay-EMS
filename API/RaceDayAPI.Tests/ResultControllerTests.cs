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
    public class ResultControllerTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static ResultController CreateController(
            RaceDayDbContext context, int organizerId, string role = "Organiser")
        {
            var controller = new ResultController(context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
                            new Claim(ClaimTypes.Role, role)
                        }, "Test"))
                }
            };

            return controller;
        }

        private static async Task SeedEventAndEnrolment(
            RaceDayDbContext context, int organizerId)
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
                EventDate = DateTime.Today.AddDays(7),
                Location = "Pretoria",
                Distance = 5,
                EventType = "Run",
                OrganizerID = organizerId
            });

            context.Categories.Add(new Category
            {
                CategoryID = 1,
                CategoryName = "Open",
                CategoryType = "Age",
                EventID = 1
            });

            context.Enrolments.Add(new Enrolment
            {
                EnrolmentID = 1,
                ParticipantID = 1,
                EventID = 1,
                CategoryID = 1
            });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateResult_InvalidFinishTime_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 10);

            var result = await controller.CreateResult(new CreateResultRequest
            {
                EnrolmentID = 1,
                FinishTime = TimeSpan.Zero,
                Position = 1
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateResult_EnrolmentDoesNotExist_ReturnsNotFound()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 10);

            var result = await controller.CreateResult(new CreateResultRequest
            {
                EnrolmentID = 999,
                FinishTime = TimeSpan.FromMinutes(30),
                Position = 1
            });

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateResult_AnotherOrganizersEvent_ReturnsForbid()
        {
            using var context = CreateContext();
            await SeedEventAndEnrolment(context, organizerId: 20);

            var controller = CreateController(context, organizerId: 10);

            var result = await controller.CreateResult(new CreateResultRequest
            {
                EnrolmentID = 1,
                FinishTime = TimeSpan.FromMinutes(30),
                Position = 1
            });

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task CreateResult_ValidResult_ReturnsCreated()
        {
            using var context = CreateContext();
            await SeedEventAndEnrolment(context, organizerId: 10);

            var controller = CreateController(context, organizerId: 10);

            var result = await controller.CreateResult(new CreateResultRequest
            {
                EnrolmentID = 1,
                FinishTime = TimeSpan.FromMinutes(30),
                Position = 1
            });

            Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Single(await context.Results.ToListAsync());
        }
    }
}