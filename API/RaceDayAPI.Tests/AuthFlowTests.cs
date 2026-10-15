using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using Xunit;

namespace RaceDayAPI.Tests
{
    // Requirement: unit tests for user registration and login (success and failure scenarios), using server-side sessions
    public class AuthFlowTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static AuthController CreateController(RaceDayDbContext context)
        {
            IConfiguration configuration = new ConfigurationBuilder().Build();

            // A simple in-memory session so the controller can store the logged-in user like it does on the server
            var httpContext = new DefaultHttpContext();
            httpContext.Features.Set<ISessionFeature>(new TestSessionFeature { Session = new TestSession() });

            return new AuthController(context, configuration)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        private static RegisterRequest NewRegisterRequest(string role, string email = "runner@example.com")
        {
            return new RegisterRequest
            {
                FirstName = "Test",
                LastName = "User",
                Email = email,
                Password = "Password123",
                Role = role
            };
        }

        // Success: a participant can register and the password is stored hashed
        [Fact]
        public async Task Register_ValidParticipant_ReturnsCreatedAndStoresHashedPassword()
        {
            using var context = CreateContext();
            var controller = CreateController(context);

            var result = await controller.Register(NewRegisterRequest("Participant"));

            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, created.StatusCode);

            var saved = Assert.Single(context.Participants);
            Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
            Assert.NotEqual("Password123", saved.PasswordHash);
        }

        // Success: an organiser can register and is saved as an organiser
        [Fact]
        public async Task Register_ValidOrganiser_SavesOrganiserRecord()
        {
            using var context = CreateContext();
            var controller = CreateController(context);

            var result = await controller.Register(NewRegisterRequest("Organiser", "organiser@example.com"));

            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, created.StatusCode);
            Assert.Single(context.Organizers);
            Assert.Empty(context.Participants);
        }

        // Failure: the same email cannot be used twice
        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = CreateController(context);

            await controller.Register(NewRegisterRequest("Participant"));
            var result = await controller.Register(NewRegisterRequest("Organiser"));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Failure: only Participant or Organiser roles are accepted
        [Fact]
        public async Task Register_InvalidRole_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = CreateController(context);

            var result = await controller.Register(NewRegisterRequest("Admin"));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.Participants);
            Assert.Empty(context.Organizers);
        }

        // Failure: passwords shorter than 8 characters are rejected
        [Fact]
        public async Task Register_ShortPassword_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = CreateController(context);
            var request = NewRegisterRequest("Participant");
            request.Password = "123";

            var result = await controller.Register(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Success: correct credentials return OK and start a server-side session holding the user id and role
        [Fact]
        public async Task Login_ValidCredentials_ReturnsOkAndStartsSession()
        {
            using var context = CreateContext();
            var controller = CreateController(context);
            await controller.Register(NewRegisterRequest("Participant"));

            var result = await controller.Login(new LoginRequest
            {
                Email = "runner@example.com",
                Password = "Password123",
                Role = "Participant"
            });

            Assert.IsType<OkObjectResult>(result);
            var session = controller.HttpContext.Session;
            Assert.Equal("Participant", session.GetString("Role"));
            Assert.False(string.IsNullOrWhiteSpace(session.GetString("UserId")));
        }

        // Success: logging out clears the server-side session
        [Fact]
        public async Task Logout_AfterLogin_ClearsSession()
        {
            using var context = CreateContext();
            var controller = CreateController(context);
            await controller.Register(NewRegisterRequest("Participant"));
            await controller.Login(new LoginRequest
            {
                Email = "runner@example.com",
                Password = "Password123",
                Role = "Participant"
            });

            var result = controller.LogoutParticipant();

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(controller.HttpContext.Session.GetString("UserId"));
            Assert.Null(controller.HttpContext.Session.GetString("Role"));
        }

        // Failure: wrong password is rejected
        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            using var context = CreateContext();
            var controller = CreateController(context);
            await controller.Register(NewRegisterRequest("Participant"));

            var result = await controller.Login(new LoginRequest
            {
                Email = "runner@example.com",
                Password = "WrongPassword",
                Role = "Participant"
            });

            Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Null(controller.HttpContext.Session.GetString("UserId"));
        }

        // Failure: an email that was never registered is rejected
        [Fact]
        public async Task Login_UnknownEmail_ReturnsUnauthorized()
        {
            using var context = CreateContext();
            var controller = CreateController(context);

            var result = await controller.Login(new LoginRequest
            {
                Email = "nobody@example.com",
                Password = "Password123",
                Role = "Participant"
            });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // Failure: a participant cannot log in through the organiser role
        [Fact]
        public async Task Login_WrongRole_ReturnsUnauthorized()
        {
            using var context = CreateContext();
            var controller = CreateController(context);
            await controller.Register(NewRegisterRequest("Participant"));

            var result = await controller.Login(new LoginRequest
            {
                Email = "runner@example.com",
                Password = "Password123",
                Role = "Organiser"
            });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // Minimal in-memory session used only by these tests
        private class TestSessionFeature : ISessionFeature
        {
            public ISession Session { get; set; } = null!;
        }

        private class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();

            public string Id { get; } = Guid.NewGuid().ToString();
            public bool IsAvailable => true;
            public IEnumerable<string> Keys => _store.Keys;

            public void Clear() => _store.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _store.Remove(key);
            public void Set(string key, byte[] value) => _store[key] = value;
            public bool TryGetValue(string key, out byte[]? value) => _store.TryGetValue(key, out value);
        }
    }
}