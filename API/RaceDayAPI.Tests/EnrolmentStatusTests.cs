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
    // Organisers can confirm or reset the status of enrolments on their own events only
    public class EnrolmentStatusTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static EnrolmentController CreateController(RaceDayDbContext context, int organizerId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
                new Claim(ClaimTypes.Role, "Organiser")
            }, "TestAuth");

            return new EnrolmentController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private static async Task<int> SeedEnrolment(RaceDayDbContext context, int ownerOrganizerId)
        {
            var raceEvent = new RaceDayAPI.Models.Event
            {
                EventName = "Pretoria Fun Run",
                Description = "A 10 km fun run",
                EventDate = DateTime.Today.AddDays(10),
                Location = "Pretoria",
                Distance = 10,
                EventType = "Run",
                OrganizerID = ownerOrganizerId
            };
            context.Events.Add(raceEvent);
            await context.SaveChangesAsync();

            var enrolment = new Enrolment
            {
                ParticipantID = 5,
                EventID = raceEvent.EventID,
                CategoryID = 1
            };
            context.Enrolments.Add(enrolment);
            await context.SaveChangesAsync();

            return enrolment.EnrolmentID;
        }

        // Success: the owning organiser can confirm an enrolment
        [Fact]
        public async Task UpdateEnrolmentStatus_ByOwningOrganiser_ConfirmsEnrolment()
        {
            using var context = CreateContext();
            int enrolmentId = await SeedEnrolment(context, 1);
            var controller = CreateController(context, 1);

            var result = await controller.UpdateEnrolmentStatus(
                enrolmentId, new UpdateEnrolmentStatusRequest { Status = "confirmed" });

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("Confirmed", context.Enrolments.Single().Status);
        }

        // Failure: another organiser cannot change the status
        [Fact]
        public async Task UpdateEnrolmentStatus_ByDifferentOrganiser_ReturnsForbid()
        {
            using var context = CreateContext();
            int enrolmentId = await SeedEnrolment(context, 1);
            var controller = CreateController(context, 2);

            var result = await controller.UpdateEnrolmentStatus(
                enrolmentId, new UpdateEnrolmentStatusRequest { Status = "Confirmed" });

            Assert.IsType<ForbidResult>(result);
            Assert.Equal("Pending", context.Enrolments.Single().Status);
        }

        // Failure: only Pending or Confirmed are accepted
        [Fact]
        public async Task UpdateEnrolmentStatus_InvalidStatus_ReturnsBadRequest()
        {
            using var context = CreateContext();
            int enrolmentId = await SeedEnrolment(context, 1);
            var controller = CreateController(context, 1);

            var result = await controller.UpdateEnrolmentStatus(
                enrolmentId, new UpdateEnrolmentStatusRequest { Status = "Cancelled" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Failure: an enrolment that does not exist
        [Fact]
        public async Task UpdateEnrolmentStatus_UnknownEnrolment_ReturnsNotFound()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 1);

            var result = await controller.UpdateEnrolmentStatus(
                999, new UpdateEnrolmentStatusRequest { Status = "Confirmed" });

            Assert.IsType<NotFoundResult>(result);
        }
    }
}