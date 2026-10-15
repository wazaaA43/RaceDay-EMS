using System;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using RaceDayAPI.Controllers;
using Xunit;

namespace RaceDayAPI.Tests
{
    // Requirement: different roles are rejected when attempting to access role-based functionality,
    // and non-public areas need authentication. ASP.NET Core enforces [Authorize(Roles = ...)] in the
    // request pipeline, so these tests verify that every protected endpoint carries the correct rule.
    public class RoleAccessTests
    {
        private static string? RolesOnMethod(Type controller, string methodName)
        {
            var method = controller.GetMethod(methodName)
                ?? throw new InvalidOperationException($"{methodName} was not found on {controller.Name}");

            return method.GetCustomAttribute<AuthorizeAttribute>()?.Roles;
        }

        // A Participant calling any of these is rejected (403) because only Organisers are allowed
        [Theory]
        [InlineData(typeof(EventController), nameof(EventController.CreateEvent))]
        [InlineData(typeof(EventController), nameof(EventController.UpdateEvent))]
        [InlineData(typeof(EventController), nameof(EventController.DeleteEvent))]
        [InlineData(typeof(CategoryController), nameof(CategoryController.CreateCategory))]
        [InlineData(typeof(CategoryController), nameof(CategoryController.UpdateCategory))]
        [InlineData(typeof(CategoryController), nameof(CategoryController.DeleteCategory))]
        [InlineData(typeof(ResultController), nameof(ResultController.GetResults))]
        [InlineData(typeof(ResultController), nameof(ResultController.CreateResult))]
        [InlineData(typeof(ResultController), nameof(ResultController.UpdateResult))]
        [InlineData(typeof(ResultController), nameof(ResultController.DeleteResult))]
        [InlineData(typeof(EnrolmentController), nameof(EnrolmentController.GetEnrolments))]
        public void OrganiserOnlyEndpoints_RequireOrganiserRole(Type controller, string methodName)
        {
            Assert.Equal("Organiser", RolesOnMethod(controller, methodName));
        }

        // An Organiser calling any of these is rejected (403) because only Participants are allowed
        [Theory]
        [InlineData(typeof(EnrolmentController), nameof(EnrolmentController.CreateEnrolment))]
        [InlineData(typeof(EnrolmentController), nameof(EnrolmentController.CancelEnrolment))]
        [InlineData(typeof(EnrolmentController), nameof(EnrolmentController.GetMyEnrolments))]
        public void ParticipantOnlyEndpoints_RequireParticipantRole(Type controller, string methodName)
        {
            Assert.Equal("Participant", RolesOnMethod(controller, methodName));
        }

        // Each profile controller is locked to its own role
        [Fact]
        public void ProfileControllers_AreRestrictedToTheirOwnRole()
        {
            Assert.Equal("Organiser", typeof(OrganizerController).GetCustomAttribute<AuthorizeAttribute>()?.Roles);
            Assert.Equal("Participant", typeof(ParticipantController).GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        }

        // Unauthenticated requests (no token) are rejected (401) on these non-public controllers
        [Fact]
        public void NonPublicControllers_RequireAuthentication()
        {
            Assert.NotNull(typeof(EnrolmentController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(ResultController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(OrganizerController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(ParticipantController).GetCustomAttribute<AuthorizeAttribute>());
        }

        // Public endpoints stay open to anonymous visitors
        [Fact]
        public void PublicEndpoints_AllowAnonymousAccess()
        {
            Assert.NotNull(typeof(EventController).GetMethod(nameof(EventController.GetEvents))!
                .GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(typeof(CategoryController).GetMethod(nameof(CategoryController.GetCategories))!
                .GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }
}