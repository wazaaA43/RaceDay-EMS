using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using Xunit;

namespace RaceDayAPI.Tests
{
    public class AuthControllerTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static IConfiguration CreateConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "test-only-signing-key-at-least-32-characters-long",
                    ["Jwt:Issuer"] = "RaceDayAPI",
                    ["Jwt:Audience"] = "RaceDayUsers",
                    ["Jwt:ExpiryMinutes"] = "60"
                })
                .Build();
        }

        [Fact]
        public async Task Register_ValidParticipant_ReturnsCreated()
        {
            using var context = CreateContext();
            var controller = new AuthController(context, CreateConfiguration());

            var request = new RegisterRequest
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "participant@example.com",
                Password = "SecurePass123!",
                Role = "Participant"
            };

            var result = await controller.Register(request);

            Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, ((ObjectResult)result).StatusCode);

            var saved = await context.Participants
                .SingleAsync(p => p.Email == "participant@example.com");

            Assert.NotEqual("SecurePass123!", saved.PasswordHash);
            Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            using var context = CreateContext();

            context.Participants.Add(new Participant
            {
                FirstName = "Existing",
                LastName = "User",
                Email = "existing@example.com",
                PasswordHash = "existing-hash"
            });

            await context.SaveChangesAsync();

            var controller = new AuthController(context, CreateConfiguration());

            var request = new RegisterRequest
            {
                FirstName = "New",
                LastName = "User",
                Email = "existing@example.com",
                Password = "SecurePass123!",
                Role = "Participant"
            };

            var result = await controller.Register(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Register_ShortPassword_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = new AuthController(context, CreateConfiguration());

            var request = new RegisterRequest
            {
                FirstName = "Test",
                LastName = "User",
                Email = "short@example.com",
                Password = "short",
                Role = "Participant"
            };

            var result = await controller.Register(request);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(await context.Participants.ToListAsync());
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsUnauthorized()
        {
            using var context = CreateContext();
            var controller = new AuthController(context, CreateConfiguration());

            var request = new LoginRequest
            {
                Email = "missing@example.com",
                Password = "SecurePass123!",
                Role = "Participant"
            };

            var result = await controller.Login(request);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}