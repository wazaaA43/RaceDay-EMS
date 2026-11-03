using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Services;
using Xunit;

namespace RaceDayAPI.Tests
{
    // Image upload endpoints: ownership, file type and file size rules (a fake storage replaces Azure)
    public class ImageUploadTests
    {
        private class FakeImageStorage : IImageStorage
        {
            public Task<string> SaveAsync(IFormFile file, string folder)
            {
                return Task.FromResult($"https://example.test/{folder}/{file.FileName}");
            }
        }

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

        private static EventController CreateEventController(RaceDayDbContext context, int organizerId)
        {
            return new EventController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreateUser(organizerId, "Organiser") }
                }
            };
        }

        // A file that starts with the PNG signature, padded to the requested length
        private static IFormFile CreatePngFile(string fileName, int length)
        {
            var bytes = new byte[length];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);

            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };
        }

        private static async Task<int> SeedEvent(RaceDayDbContext context, int organizerId)
        {
            var raceEvent = new RaceDayAPI.Models.Event
            {
                EventName = "Pretoria Fun Run",
                Description = "A 10 km fun run",
                EventDate = DateTime.Today.AddDays(10),
                Location = "Pretoria",
                Distance = 10,
                EventType = "Run",
                OrganizerID = organizerId
            };
            context.Events.Add(raceEvent);
            await context.SaveChangesAsync();

            return raceEvent.EventID;
        }

        // Success: the owning organiser uploads a valid banner and its URL is saved
        [Fact]
        public async Task UploadBanner_ByOwningOrganiser_SavesBannerUrl()
        {
            using var context = CreateContext();
            int eventId = await SeedEvent(context, 1);
            var controller = CreateEventController(context, 1);

            var result = await controller.UploadBanner(eventId, CreatePngFile("banner.png", 100), new FakeImageStorage());

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("https://example.test/banners/banner.png", context.Events.Single().BannerUrl);
        }

        // Failure: another organiser cannot upload a banner
        [Fact]
        public async Task UploadBanner_ByDifferentOrganiser_ReturnsForbid()
        {
            using var context = CreateContext();
            int eventId = await SeedEvent(context, 1);
            var controller = CreateEventController(context, 2);

            var result = await controller.UploadBanner(eventId, CreatePngFile("banner.png", 100), new FakeImageStorage());

            Assert.IsType<ForbidResult>(result);
            Assert.Null(context.Events.Single().BannerUrl);
        }

        // Failure: a file that is not really an image is rejected
        [Fact]
        public async Task UploadBanner_NotAnImage_ReturnsBadRequest()
        {
            using var context = CreateContext();
            int eventId = await SeedEvent(context, 1);
            var controller = CreateEventController(context, 1);

            var textBytes = System.Text.Encoding.UTF8.GetBytes("this is just text, not a picture");
            IFormFile fake = new FormFile(new MemoryStream(textBytes), 0, textBytes.Length, "file", "banner.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            var result = await controller.UploadBanner(eventId, fake, new FakeImageStorage());

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Null(context.Events.Single().BannerUrl);
        }

        // Failure: a file over 2 MB is rejected
        [Fact]
        public async Task UploadBanner_TooLarge_ReturnsBadRequest()
        {
            using var context = CreateContext();
            int eventId = await SeedEvent(context, 1);
            var controller = CreateEventController(context, 1);

            var result = await controller.UploadBanner(
                eventId, CreatePngFile("banner.png", 3 * 1024 * 1024), new FakeImageStorage());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Success: a participant uploads their own profile picture
        [Fact]
        public async Task UploadProfilePicture_ValidImage_SavesUrl()
        {
            using var context = CreateContext();
            context.Participants.Add(new Participant
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "participant@example.com",
                PasswordHash = "test-hash"
            });
            await context.SaveChangesAsync();
            int participantId = context.Participants.Single().ParticipantID;

            var controller = new ParticipantController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreateUser(participantId, "Participant") }
                }
            };

            var result = await controller.UploadProfilePicture(CreatePngFile("me.png", 100), new FakeImageStorage());

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("https://example.test/profiles/me.png", context.Participants.Single().ProfilePictureUrl);
        }
    }
}