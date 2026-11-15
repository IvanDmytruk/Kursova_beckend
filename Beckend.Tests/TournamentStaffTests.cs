
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.TournamentStaffs;
using Beckend.Services.TournamentAccess;
using Moq;
using Xunit;

using StaffModel = Beckend.Models.TournamentStaff;

namespace Beckend.Tests.TournamentStaffTests;

public class TournamentPermissionLifecycleTests
{
    private const string TournamentId = "tournament-1";
    private const string OrganizerId = "organizer-1";
    private const string ManagerId = "manager-1";

    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<ITournamentStaffRepository> _staff = new();

    private readonly TournamentAccessService _access;

    private StaffModel? _currentManager;

    public TournamentPermissionLifecycleTests()
    {
        _access = new TournamentAccessService(
            _tournaments.Object,
            _staff.Object);

        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync(new Tournament
            {
                Id = TournamentId,
                CreatedBy = OrganizerId
            });

        _staff
            .Setup(r => r.GetActiveAsync(
                TournamentId,
                ManagerId))
            .ReturnsAsync(() =>
                _currentManager?.IsActive == true
                    ? _currentManager
                    : null);

        _staff
            .Setup(r => r.AssignAsync(
                It.IsAny<StaffModel>()))
            .ReturnsAsync((StaffModel manager) =>
            {
                if (_currentManager?.IsActive == true)
                    throw new InvalidOperationException(
                        "Manager already active.");

                _currentManager = manager;
                return manager;
            });

        _staff
            .Setup(r => r.UpdatePermissionsAsync(
                TournamentId,
                ManagerId,
                It.IsAny<TournamentPermission>()))
            .ReturnsAsync((string _, string _,
                TournamentPermission permissions) =>
            {
                if (_currentManager?.IsActive != true)
                    return false;

                _currentManager.Permissions = permissions;
                return true;
            });

        _staff
            .Setup(r => r.RevokeAsync(
                TournamentId,
                ManagerId))
            .ReturnsAsync(() =>
            {
                if (_currentManager?.IsActive != true)
                    return false;

                _currentManager.IsActive = false;
                _currentManager.RevokedAt = DateTime.UtcNow;
                return true;
            });
    }

    private async Task AssignAsync(
        TournamentPermission permissions)
    {
        await _staff.Object.AssignAsync(new StaffModel
        {
            TournamentId = TournamentId,
            UserId = ManagerId,
            Role = TournamentMemberRole.Manager,
            Permissions = permissions,
            AssignedBy = OrganizerId,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        });
    }

    private Task<bool> CanAsync(
        TournamentPermission permission)
    {
        return _access.HasPermissionAsync(
            TournamentId,
            ManagerId,
            permission);
    }

    [Fact]
    public async Task RevokedManager_LosesAccessImmediately()
    {
        await AssignAsync(
            TournamentPermission.ManageMatches);

        Assert.True(await CanAsync(
            TournamentPermission.ManageMatches));

        Assert.True(await _staff.Object.RevokeAsync(
            TournamentId,
            ManagerId));

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches));
    }

    [Fact]
    public async Task UpdatedPermissions_TakeEffectImmediately()
    {
        await AssignAsync(
            TournamentPermission.ManageMatches);

        Assert.True(await CanAsync(
            TournamentPermission.ManageMatches));

        await _staff.Object.UpdatePermissionsAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ApproveResults);

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches));

        Assert.True(await CanAsync(
            TournamentPermission.ApproveResults));
    }

    [Fact]
    public async Task RemovedPermissions_DenyAllOperations()
    {
        await AssignAsync(TournamentPermission.All);

        await _staff.Object.UpdatePermissionsAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.None);

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches));

        Assert.False(await CanAsync(
            TournamentPermission.ApproveResults));

        Assert.False(await CanAsync(
            TournamentPermission.SetTechnicalResults));

        Assert.False(await CanAsync(
            TournamentPermission.ViewStaff));
    }

    [Fact]
    public async Task ReassignedManager_GetsOnlyNewPermissions()
    {
        await AssignAsync(TournamentPermission.All);

        await _staff.Object.RevokeAsync(
            TournamentId,
            ManagerId);

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches));

        await AssignAsync(
            TournamentPermission.ViewStaff);

        Assert.True(await CanAsync(
            TournamentPermission.ViewStaff));

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches));

        Assert.False(await CanAsync(
            TournamentPermission.ApproveResults));
    }

    [Fact]
    public async Task RevokedManager_CannotUpdatePermissions()
    {
        await AssignAsync(
            TournamentPermission.ManageMatches);

        await _staff.Object.RevokeAsync(
            TournamentId,
            ManagerId);

        var updated = await _staff.Object
            .UpdatePermissionsAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.All);

        Assert.False(updated);

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches));
    }

    [Fact]
    public async Task DuplicateActiveAssignment_IsRejected()
    {
        await AssignAsync(
            TournamentPermission.ManageMatches);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AssignAsync(
                TournamentPermission.ApproveResults));
    }

    [Fact]
    public async Task RevokedManager_DoesNotKeepCombinedPermissions()
    {
        await AssignAsync(
            TournamentPermission.ManageMatches |
            TournamentPermission.ApproveResults);

        Assert.True(await CanAsync(
            TournamentPermission.ManageMatches |
            TournamentPermission.ApproveResults));

        await _staff.Object.RevokeAsync(
            TournamentId,
            ManagerId);

        Assert.False(await CanAsync(
            TournamentPermission.ManageMatches |
            TournamentPermission.ApproveResults));
    }
}
