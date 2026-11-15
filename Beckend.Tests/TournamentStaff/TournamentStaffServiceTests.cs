
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.TournamentStaffs;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.Users;
using Beckend.Services.TournamentAccess;
using Beckend.Services.TournamentStaff;
using Moq;
using Xunit;

using StaffModel = Beckend.Models.TournamentStaff;

namespace Beckend.Tests.TournamentStaffTests;

public class TournamentStaffServiceTests
{
    private const string TournamentId = "tournament-1";
    private const string OrganizerId = "organizer-1";
    private const string ManagerId = "manager-1";
    private const string OtherUserId = "other-user";

    private readonly Mock<ITournamentStaffRepository> _staff = new();
    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITournamentAccessService> _access = new();

    private readonly TournamentStaffService _service;

    public TournamentStaffServiceTests()
    {
        _service = new TournamentStaffService(
            _staff.Object,
            _tournaments.Object,
            _users.Object,
            _access.Object);

        SetupTournament();
        SetupUser(OrganizerId);
        SetupUser(ManagerId);
        SetupUser(OtherUserId);
    }

    private void SetupTournament(
        string createdBy = OrganizerId)
    {
        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync(new Tournament
            {
                Id = TournamentId,
                CreatedBy = createdBy
            });
    }

    private void SetupUser(
        string userId,
        bool isBanned = false,
        DateTime? bannedUntil = null)
    {
        _users
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(new User
            {
                Id = userId,
                Role = UserRole.User,
                IsBanned = isBanned,
                BannedUntil = bannedUntil
            });
    }

    private static StaffModel CreateManager(
        TournamentPermission permissions =
            TournamentPermission.ManageMatches)
    {
        return new StaffModel
        {
            TournamentId = TournamentId,
            UserId = ManagerId,
            Role = TournamentMemberRole.Manager,
            Permissions = permissions,
            AssignedBy = OrganizerId,
            IsActive = true
        };
    }

    private void SetupActiveManager(
        TournamentPermission permissions =
            TournamentPermission.ManageMatches)
    {
        _staff
            .Setup(r => r.GetActiveAsync(
                TournamentId,
                ManagerId))
            .ReturnsAsync(CreateManager(permissions));
    }

    [Fact]
    public async Task AssignManager_ValidData_CreatesManager()
    {
        var permissions =
            TournamentPermission.ManageMatches |
            TournamentPermission.ViewStaff;

        _staff
            .Setup(r => r.AssignAsync(
                It.IsAny<StaffModel>()))
            .ReturnsAsync((StaffModel s) => s);

        var result = await _service.AssignManagerAsync(
            TournamentId,
            ManagerId,
            permissions,
            OrganizerId);

        Assert.Equal(TournamentId, result.TournamentId);
        Assert.Equal(ManagerId, result.UserId);
        Assert.Equal(OrganizerId, result.AssignedBy);
        Assert.Equal(TournamentMemberRole.Manager, result.Role);
        Assert.Equal(permissions, result.Permissions);
        Assert.True(result.IsActive);

        _staff.Verify(r => r.AssignAsync(
            It.Is<StaffModel>(s =>
                s.UserId == ManagerId &&
                s.TournamentId == TournamentId &&
                s.Permissions == permissions)),
            Times.Once);
    }

    [Fact]
    public async Task AssignManager_NonOrganizer_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                OtherUserId));

        _staff.Verify(r => r.AssignAsync(
            It.IsAny<StaffModel>()), Times.Never);
    }

    [Fact]
    public async Task AssignManager_MissingTournament_Throws()
    {
        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync((Tournament?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                OrganizerId));
    }

    [Fact]
    public async Task AssignManager_MissingUser_Throws()
    {
        _users
            .Setup(r => r.GetByIdAsync(ManagerId))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                OrganizerId));
    }

    [Fact]
    public async Task AssignManager_DuplicateManager_Throws()
    {
        SetupActiveManager();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                OrganizerId));

        _staff.Verify(r => r.AssignAsync(
            It.IsAny<StaffModel>()), Times.Never);
    }

    [Fact]
    public async Task AssignManager_OrganizerAsManager_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                OrganizerId,
                TournamentPermission.ManageMatches,
                OrganizerId));
    }

    [Fact]
    public async Task AssignManager_BannedManager_Throws()
    {
        SetupUser(
            ManagerId,
            isBanned: true,
            bannedUntil: DateTime.UtcNow.AddHours(1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                OrganizerId));
    }

    [Fact]
    public async Task AssignManager_BannedOrganizer_Throws()
    {
        SetupUser(
            OrganizerId,
            isBanned: true,
            bannedUntil: DateTime.UtcNow.AddHours(1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                OrganizerId));
    }

    [Fact]
    public async Task AssignManager_InvalidPermissions_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                (TournamentPermission)(1 << 20),
                OrganizerId));
    }

    [Fact]
    public async Task AssignManager_EmptyOrganizerId_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches,
                ""));
    }

    [Fact]
    public async Task AssignManager_EmptyManagerId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AssignManagerAsync(
                TournamentId,
                "",
                TournamentPermission.ManageMatches,
                OrganizerId));
    }

    [Fact]
    public async Task UpdatePermissions_ValidManager_Succeeds()
    {
        SetupActiveManager();

        var permissions =
            TournamentPermission.ApproveResults |
            TournamentPermission.SetTechnicalResults;

        _staff
            .Setup(r => r.UpdatePermissionsAsync(
                TournamentId,
                ManagerId,
                permissions))
            .ReturnsAsync(true);

        var result =
            await _service.UpdateManagerPermissionsAsync(
                TournamentId,
                ManagerId,
                permissions,
                OrganizerId);

        Assert.True(result);

        _staff.Verify(r => r.UpdatePermissionsAsync(
            TournamentId,
            ManagerId,
            permissions), Times.Once);
    }

    [Fact]
    public async Task UpdatePermissions_NonOrganizer_Throws()
    {
        SetupActiveManager();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateManagerPermissionsAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ApproveResults,
                OtherUserId));
    }

    [Fact]
    public async Task UpdatePermissions_MissingManager_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateManagerPermissionsAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ApproveResults,
                OrganizerId));
    }

    [Fact]
    public async Task UpdatePermissions_InvalidPermissions_Throws()
    {
        SetupActiveManager();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateManagerPermissionsAsync(
                TournamentId,
                ManagerId,
                (TournamentPermission)(1 << 20),
                OrganizerId));
    }

    [Fact]
    public async Task UpdatePermissions_None_IsAllowed()
    {
        SetupActiveManager();

        _staff
            .Setup(r => r.UpdatePermissionsAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.None))
            .ReturnsAsync(true);

        var result =
            await _service.UpdateManagerPermissionsAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.None,
                OrganizerId);

        Assert.True(result);
    }

    [Fact]
    public async Task RevokeManager_ValidManager_Succeeds()
    {
        SetupActiveManager();

        _staff
            .Setup(r => r.RevokeAsync(
                TournamentId,
                ManagerId))
            .ReturnsAsync(true);

        var result = await _service.RevokeManagerAsync(
            TournamentId,
            ManagerId,
            OrganizerId);

        Assert.True(result);

        _staff.Verify(r => r.RevokeAsync(
            TournamentId,
            ManagerId), Times.Once);
    }

    [Fact]
    public async Task RevokeManager_NonOrganizer_Throws()
    {
        SetupActiveManager();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RevokeManagerAsync(
                TournamentId,
                ManagerId,
                OtherUserId));
    }

    [Fact]
    public async Task RevokeManager_MissingManager_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RevokeManagerAsync(
                TournamentId,
                ManagerId,
                OrganizerId));
    }

    [Fact]
    public async Task GetManagers_WithViewStaff_ReturnsManagers()
    {
        var managers = new List<StaffModel>
        {
            CreateManager(TournamentPermission.ViewStaff)
        };

        _access
            .Setup(a => a.HasPermissionAsync(
                TournamentId,
                OrganizerId,
                TournamentPermission.ViewStaff))
            .ReturnsAsync(true);

        _staff
            .Setup(r => r.GetByTournamentAsync(TournamentId))
            .ReturnsAsync(managers);

        var result = await _service.GetManagersAsync(
            TournamentId,
            OrganizerId);

        Assert.Single(result);
        Assert.Equal(ManagerId, result[0].UserId);
    }

    [Fact]
    public async Task GetManagers_WithoutViewStaff_Throws()
    {
        _access
            .Setup(a => a.HasPermissionAsync(
                TournamentId,
                OtherUserId,
                TournamentPermission.ViewStaff))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetManagersAsync(
                TournamentId,
                OtherUserId));
    }

    [Fact]
    public async Task GetManagers_BannedRequester_Throws()
    {
        SetupUser(
            OtherUserId,
            isBanned: true,
            bannedUntil: DateTime.UtcNow.AddHours(1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetManagersAsync(
                TournamentId,
                OtherUserId));

        _access.Verify(a => a.HasPermissionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TournamentPermission>()),
            Times.Never);
    }

    [Fact]
    public async Task GetManagers_EmptyRequester_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetManagersAsync(
                TournamentId,
                ""));
    }

    [Fact]
    public async Task GetManagers_EmptyTournamentId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetManagersAsync(
                "",
                OrganizerId));
    }

    [Fact]
    public async Task GetManagers_MissingRequester_Throws()
    {
        _users
            .Setup(r => r.GetByIdAsync(OtherUserId))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetManagersAsync(
                TournamentId,
                OtherUserId));
    }
}
