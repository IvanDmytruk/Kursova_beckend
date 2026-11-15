
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.TournamentStaffs;
using Beckend.Repositories.Tournaments;
using Beckend.Services.TournamentAccess;
using Moq;
using Xunit;

using StaffModel = Beckend.Models.TournamentStaff;

namespace Beckend.Tests.TournamentStaffTests;

public class TournamentAccessServiceTests
{
    private const string TournamentId = "tournament-1";
    private const string OrganizerId = "organizer-1";
    private const string ManagerId = "manager-1";

    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<ITournamentStaffRepository> _staff = new();

    private readonly TournamentAccessService _service;

    public TournamentAccessServiceTests()
    {
        _service = new TournamentAccessService(
            _tournaments.Object,
            _staff.Object);

        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync(new Tournament
            {
                Id = TournamentId,
                CreatedBy = OrganizerId
            });
    }

    private void SetupManager(
        TournamentPermission permissions,
        bool active = true)
    {
        _staff
            .Setup(r => r.GetActiveAsync(
                TournamentId,
                ManagerId))
            .ReturnsAsync(active
                ? new StaffModel
                {
                    TournamentId = TournamentId,
                    UserId = ManagerId,
                    Role = TournamentMemberRole.Manager,
                    Permissions = permissions,
                    IsActive = true
                }
                : null);
    }

    [Fact]
    public async Task Organizer_HasViewStaffPermission()
    {
        var result = await _service.HasPermissionAsync(
            TournamentId,
            OrganizerId,
            TournamentPermission.ViewStaff);

        Assert.True(result);
    }

    [Fact]
    public async Task Manager_WithViewStaff_HasPermission()
    {
        SetupManager(TournamentPermission.ViewStaff);

        var result = await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ViewStaff);

        Assert.True(result);
    }

    [Fact]
    public async Task Manager_WithoutViewStaff_IsDenied()
    {
        SetupManager(TournamentPermission.ViewAuditLog);

        var result = await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ViewStaff);

        Assert.False(result);
    }

    [Fact]
    public async Task Manager_WithCombinedPermissions_HasAllRequested()
    {
        SetupManager(
            TournamentPermission.ViewStaff |
            TournamentPermission.ManageMatches);

        var result = await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ViewStaff |
            TournamentPermission.ManageMatches);

        Assert.True(result);
    }

    [Fact]
    public async Task Manager_MissingOneRequestedPermission_IsDenied()
    {
        SetupManager(TournamentPermission.ViewStaff);

        var result = await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ViewStaff |
            TournamentPermission.ManageMatches);

        Assert.False(result);
    }

    [Fact]
    public async Task InactiveManager_IsDenied()
    {
        SetupManager(
            TournamentPermission.ViewStaff,
            active: false);

        var result = await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ViewStaff);

        Assert.False(result);
    }

    [Fact]
    public async Task UnknownPermission_IsDenied()
    {
        var result = await _service.HasPermissionAsync(
            TournamentId,
            OrganizerId,
            (TournamentPermission)(1 << 20));

        Assert.False(result);
    }

    [Fact]
    public async Task NonePermission_IsDenied()
    {
        var result = await _service.HasPermissionAsync(
            TournamentId,
            OrganizerId,
            TournamentPermission.None);

        Assert.False(result);
    }

    [Fact]
    public async Task EmptyUserId_IsDenied()
    {
        var result = await _service.HasPermissionAsync(
            TournamentId,
            "",
            TournamentPermission.ViewStaff);

        Assert.False(result);
    }

    [Fact]
    public async Task MissingTournament_IsDenied()
    {
        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync((Tournament?)null);

        var result = await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ViewStaff);

        Assert.False(result);
    }

[Fact]
public async Task RevokedManager_LosesAccessImmediately()
{
    var active = true;

    _staff.Setup(r => r.GetActiveAsync(
            TournamentId,
            ManagerId))
        .ReturnsAsync(() => active
            ? new StaffModel
            {
                TournamentId = TournamentId,
                UserId = ManagerId,
                Role = TournamentMemberRole.Manager,
                Permissions = TournamentPermission.ManageMatches,
                IsActive = true
            }
            : null);

    var beforeRevocation =
        await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ManageMatches);

    active = false;

    var afterRevocation =
        await _service.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ManageMatches);

    Assert.True(beforeRevocation);
    Assert.False(afterRevocation);

    _staff.Verify(r => r.GetActiveAsync(
        TournamentId,
        ManagerId), Times.Exactly(2));
}
}
