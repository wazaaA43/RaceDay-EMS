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
    public class CategoryControllerTests
    {
        private static RaceDayDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RaceDayDbContext(options);
        }

        private static CategoryController CreateController(
            RaceDayDbContext context, int organizerId)
        {
            var controller = new CategoryController(context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
                            new Claim(ClaimTypes.Role, "Organiser")
                        }, "Test"))
                }
            };

            return controller;
        }

        [Fact]
        public async Task GetCategories_ReturnsSavedCategories()
        {
            using var context = CreateContext();

            context.Events.Add(new RaceDayAPI.Models.Event
            {
                EventID = 1,
                EventName = "Test Run",
                Description = "Test event",
                EventDate = DateTime.Today.AddDays(7),
                Location = "Pretoria",
                Distance = 5,
                EventType = "Run",
                OrganizerID = 1
            });

            context.Categories.Add(new Category
            {
                CategoryID = 1,
                CategoryName = "Open",
                CategoryType = "Age",
                EventID = 1
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, 1);

            var result = await controller.GetCategories();

            Assert.NotNull(result.Value);
            Assert.Single(result.Value);
            Assert.Equal("Open", result.Value.First().CategoryName);
        }

        [Fact]
        public async Task CreateCategory_MissingName_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 1);

            var category = new CreateCategoryRequest
            {
                CategoryName = "",
                CategoryType = "Age",
                EventID = 1
            };

            var result = await controller.CreateCategory(category);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateCategory_EventNotFound_ReturnsNotFound()
        {
            using var context = CreateContext();
            var controller = CreateController(context, 1);

            var category = new CreateCategoryRequest
            {
                CategoryName = "Open",
                CategoryType = "Age",
                EventID = 999
            };

            var result = await controller.CreateCategory(category);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateCategory_EventOwnedByAnotherOrganizer_ReturnsForbid()
        {
            using var context = CreateContext();

            context.Events.Add(new RaceDayAPI.Models.Event
            {
                EventName = "Test Run",
                Description = "Test event",
                EventDate = DateTime.Today.AddDays(7),
                Location = "Pretoria",
                Distance = 5,
                EventType = "Run",
                OrganizerID = 2
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, 1);

            var category = new CreateCategoryRequest
            {
                CategoryName = "Open",
                CategoryType = "Age",
                EventID = 1
            };

            var result = await controller.CreateCategory(category);

            Assert.IsType<ForbidResult>(result.Result);
        }
    }
}