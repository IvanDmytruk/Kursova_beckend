
using System.Reflection;
using Beckend.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Beckend.Tests.Security;

public sealed class ControllerAuthorizationTests
{
    public static IEnumerable<object[]> ProtectedActions()
    {
        yield return new object[]
        {
            typeof(UserController), "Create", "Admin"
        };

        yield return new object[]
        {
            typeof(UserController), "Update", "Admin"
        };

        yield return new object[]
        {
            typeof(UserController), "Delete", "Admin"
        };

        yield return new object[]
        {
            typeof(UserController), "GetAll", "Admin"
        };

        yield return new object[]
        {
            typeof(UserController), "GetById", "Admin"
        };

        yield return new object[]
        {
            typeof(StatisticsController), "Create", "Admin"
        };

        yield return new object[]
        {
            typeof(StatisticsController), "Update", "Admin"
        };

        yield return new object[]
        {
            typeof(StatisticsController), "Delete", "Admin"
        };

        yield return new object[]
        {
            typeof(StatisticsController), "UpdateMatchStats", "Admin"
        };

        yield return new object[]
        {
            typeof(SportsController), "Create", "Admin"
        };

        yield return new object[]
        {
            typeof(SportsController), "Update", "Admin"
        };

        yield return new object[]
        {
            typeof(SportsController), "Delete", "Admin"
        };

        yield return new object[]
        {
            typeof(SportsController), "ToggleActive", "Admin"
        };

        yield return new object[]
        {
            typeof(TeamMembershipController), "Invite", ""
        };

        yield return new object[]
        {
            typeof(TournamentParticipationRequestsController),
            "Accept", ""
        };

        yield return new object[]
        {
            typeof(TournamentStaffController),
            "AssignManager", ""
        };
    }

    [Theory]
    [MemberData(nameof(ProtectedActions))]
    public void ProtectedAction_RequiresAuthorization(
        Type controllerType,
        string actionName,
        string requiredRole)
    {
        var method = controllerType.GetMethod(
            actionName,
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);

        var allowAnonymous =
            method.GetCustomAttribute<AllowAnonymousAttribute>();

        Assert.Null(allowAnonymous);

        var authorizations = controllerType
            .GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(
                method.GetCustomAttributes<AuthorizeAttribute>(true))
            .ToList();

        Assert.NotEmpty(authorizations);

        if (!string.IsNullOrWhiteSpace(requiredRole))
        {
            Assert.Contains(authorizations, attribute =>
                (attribute.Roles ?? "")
                    .Split(',', StringSplitOptions.TrimEntries)
                    .Contains(requiredRole));
        }
    }

    [Theory]
    [InlineData(nameof(UserController.SearchPlCo))]
    [InlineData(nameof(UserController.SearchPlCoByID))]
    [InlineData(nameof(UserController.GetByAgeRange))]
    public void PublicPlayerSearch_AllowsAnonymous(
        string methodName)
    {
        var method = typeof(UserController).GetMethod(methodName);

        Assert.NotNull(method);

        Assert.NotNull(
            method.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
