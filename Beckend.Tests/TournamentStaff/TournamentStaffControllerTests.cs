using Beckend.Models;
using Beckend.Repositories.TournamentStaffs;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beckend.Controllers;
using Beckend.DTOs.TournamentStaff;
using Beckend.Enums;
using Beckend.Services.TournamentStaff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

using StaffModel = Beckend.Models.TournamentStaff;

namespace Beckend.Tests.TournamentStaffTests;

public class TournamentStaffControllerTests
{
    private const string TournamentId = "tournament-1";
    private const string OrganizerId = "organizer-1";
    private const string ManagerId = "manager-1";


private readonly Mock<ITournamentStaffService> _service = new();

private readonly Mock<ITournamentStaffRepository> _staffRepository = new();

private readonly Mock<IMongoCollection<Tournament>> _tournaments = new();

private readonly TournamentStaffController _controller;


public TournamentStaffControllerTests()
{
    var databaseMock = new Mock<IMongoDatabase>();

    databaseMock
        .Setup(db => db.GetCollection<Tournament>(
            "Tournaments",
            It.IsAny<MongoCollectionSettings>()))
        .Returns(_tournaments.Object);

    _controller = new TournamentStaffController(
        _service.Object,
        _staffRepository.Object,
        databaseMock.Object
    );

    SetUser(OrganizerId);
}



    private void SetUser(string? userId)
    {
        var claims = new List<Claim>();

        if (userId != null)
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.Sub, userId));
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static StaffModel CreateManager()
    {
        return new StaffModel
        {
            TournamentId = TournamentId,
            UserId = ManagerId,
            AssignedBy = OrganizerId,
            Role = TournamentMemberRole.Manager,
            Permissions = TournamentPermission.ManageMatches,
            IsActive = true
        };
    }

    [Fact]
    public async Task AssignManager_ValidRequest_Returns201()
    {
        var dto = new AssignManagerDto
        {
            UserId = ManagerId,
            Permissions = TournamentPermission.ManageMatches
        };

        _service.Setup(s => s.AssignManagerAsync(
                TournamentId,
                ManagerId,
                dto.Permissions,
                OrganizerId))
            .ReturnsAsync(CreateManager());

        var result = await _controller.AssignManager(
            TournamentId, dto);

        var created = Assert.IsType<ObjectResult>(result);

        Assert.Equal(201, created.StatusCode);

        var manager = Assert.IsType<StaffModel>(created.Value);

        Assert.Equal(ManagerId, manager.UserId);

        _service.Verify(s => s.AssignManagerAsync(
            TournamentId,
            ManagerId,
            dto.Permissions,
            OrganizerId), Times.Once);
    }

    [Fact]
    public async Task AssignManager_NoUserId_Returns401()
    {
        SetUser(null);

        var result = await _controller.AssignManager(
            TournamentId,
            new AssignManagerDto { UserId = ManagerId });

        Assert.IsType<UnauthorizedResult>(result);

        _service.Verify(s => s.AssignManagerAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TournamentPermission>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AssignManager_NotOrganizer_Returns403()
    {
        _service.Setup(s => s.AssignManagerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TournamentPermission>(),
                OrganizerId))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.AssignManager(
            TournamentId,
            new AssignManagerDto { UserId = ManagerId });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AssignManager_Duplicate_Returns409()
    {
        _service.Setup(s => s.AssignManagerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TournamentPermission>(),
                OrganizerId))
            .ThrowsAsync(new InvalidOperationException(
                "Manager already assigned."));

        var result = await _controller.AssignManager(
            TournamentId,
            new AssignManagerDto { UserId = ManagerId });

        var conflict = Assert.IsType<ConflictObjectResult>(result);

        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task AssignManager_MissingTournament_Returns404()
    {
        _service.Setup(s => s.AssignManagerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TournamentPermission>(),
                OrganizerId))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.AssignManager(
            TournamentId,
            new AssignManagerDto { UserId = ManagerId });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AssignManager_InvalidPermissions_Returns400()
    {
        _service.Setup(s => s.AssignManagerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TournamentPermission>(),
                OrganizerId))
            .ThrowsAsync(new ArgumentException(
                "Invalid permissions."));

        var result = await _controller.AssignManager(
            TournamentId,
            new AssignManagerDto
            {
                UserId = ManagerId,
                Permissions = (TournamentPermission)(1 << 20)
            });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetManagers_Authorized_Returns200()
    {
        var managers = new List<StaffModel>
        {
            CreateManager()
        };

        _service.Setup(s => s.GetManagersAsync(
                TournamentId, OrganizerId))
            .ReturnsAsync(managers);

        var result = await _controller.GetManagers(TournamentId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<StaffModel>>(ok.Value);

        Assert.Single(returned);
        Assert.Equal(ManagerId, returned[0].UserId);
    }

    [Fact]
    public async Task GetManagers_Forbidden_Returns403()
    {
        _service.Setup(s => s.GetManagersAsync(
                TournamentId, OrganizerId))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.GetManagers(TournamentId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetManagers_MissingTournament_Returns404()
    {
        _service.Setup(s => s.GetManagersAsync(
                TournamentId, OrganizerId))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.GetManagers(TournamentId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePermissions_Success_Returns204()
    {
        var dto = new UpdateManagerPermissionsDto
        {
            Permissions = TournamentPermission.ViewStaff
        };

        _service.Setup(s => s.UpdateManagerPermissionsAsync(
                TournamentId,
                ManagerId,
                dto.Permissions,
                OrganizerId))
            .ReturnsAsync(true);

        var result = await _controller.UpdatePermissions(
            TournamentId, ManagerId, dto);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdatePermissions_Forbidden_Returns403()
    {
        _service.Setup(s => s.UpdateManagerPermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TournamentPermission>(),
                OrganizerId))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.UpdatePermissions(
            TournamentId,
            ManagerId,
            new UpdateManagerPermissionsDto());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdatePermissions_MissingManager_Returns404()
    {
        _service.Setup(s => s.UpdateManagerPermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TournamentPermission>(),
                OrganizerId))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.UpdatePermissions(
            TournamentId,
            ManagerId,
            new UpdateManagerPermissionsDto());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RevokeManager_Success_Returns204()
    {
        _service.Setup(s => s.RevokeManagerAsync(
                TournamentId, ManagerId, OrganizerId))
            .ReturnsAsync(true);

        var result = await _controller.RevokeManager(
            TournamentId, ManagerId);

        Assert.IsType<NoContentResult>(result);

        _service.Verify(s => s.RevokeManagerAsync(
            TournamentId, ManagerId, OrganizerId), Times.Once);
    }

    [Fact]
    public async Task RevokeManager_Forbidden_Returns403()
    {
        _service.Setup(s => s.RevokeManagerAsync(
                TournamentId, ManagerId, OrganizerId))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.RevokeManager(
            TournamentId, ManagerId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RevokeManager_MissingManager_Returns404()
    {
        _service.Setup(s => s.RevokeManagerAsync(
                TournamentId, ManagerId, OrganizerId))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.RevokeManager(
            TournamentId, ManagerId);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
