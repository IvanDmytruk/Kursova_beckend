using Beckend.Enums;
using Beckend.Models;
using TournamentStaffModel = Beckend.Models.TournamentStaff;
using Beckend.Repositories.TournamentStaffs;
using Beckend.Repositories.Tournaments;
using Beckend.Services.TournamentAccess;
using Moq;
using MongoDB.Bson;
using Xunit;

namespace Beckend.Tests.TournamentStaff;

public sealed class TournamentStaffAccessTests
{
    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<ITournamentStaffRepository> _staff = new();

    private readonly TournamentAccessService _service;

    private readonly string _tournamentId =
        ObjectId.GenerateNewId().ToString();

    private readonly string _organizerId =
        ObjectId.GenerateNewId().ToString();

    private readonly string _managerId =
        ObjectId.GenerateNewId().ToString();

    public TournamentStaffAccessTests()
    {
        _service = new TournamentAccessService(
            _tournaments.Object,
            _staff.Object);

        _tournaments.Setup(x => x.GetByIdAsync(
                It.IsAny<string>()))
            .ReturnsAsync((string id) => id == _tournamentId
                ? new Tournament
                {
                    Id = _tournamentId,
                    CreatedBy = _organizerId
                }
                : null);

        _staff.Setup(x => x.GetActiveAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(
                (string tournamentId, string userId) =>
                {
                    if (tournamentId != _tournamentId ||
                        userId != _managerId)
                        return null;

                    return new TournamentStaffModel
                    {
                        TournamentId = _tournamentId,
                        UserId = _managerId,
                        Role = TournamentMemberRole.Manager,
                        Permissions =
                            TournamentPermission.ManageMatches |
                            TournamentPermission.ApproveResults,
                        IsActive = true
                    };
                });
    }

    [Fact]
    public async Task Organizer_HasAllAvailablePermissions()
    {
        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _organizerId,
            TournamentPermission.ManageParticipants);

        Assert.True(result);
    }

    [Fact]
    public async Task Manager_HasAssignedPermission()
    {
        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _managerId,
            TournamentPermission.ManageMatches);

        Assert.True(result);
    }

    [Fact]
    public async Task Manager_HasCombinedAssignedPermissions()
    {
        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _managerId,
            TournamentPermission.ManageMatches |
            TournamentPermission.ApproveResults);

        Assert.True(result);
    }

    [Fact]
    public async Task Manager_CannotUseUnassignedPermission()
    {
        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _managerId,
            TournamentPermission.ManageParticipants);

        Assert.False(result);
    }

    [Fact]
    public async Task Manager_CannotUsePermissionInOtherTournament()
    {
        var otherTournamentId =
            ObjectId.GenerateNewId().ToString();

        var result = await _service.HasPermissionAsync(
            otherTournamentId,
            _managerId,
            TournamentPermission.ManageMatches);

        Assert.False(result);
    }

    [Fact]
    public async Task UnknownUser_HasNoPermissions()
    {
        var userId = ObjectId.GenerateNewId().ToString();

        var result = await _service.HasPermissionAsync(
            _tournamentId,
            userId,
            TournamentPermission.ManageMatches);

        Assert.False(result);
    }

    [Fact]
    public async Task RevokedManager_HasNoPermissions()
    {
        _staff.Setup(x => x.GetActiveAsync(
                _tournamentId,
                _managerId))
            .ReturnsAsync(new TournamentStaffModel
            {
                TournamentId = _tournamentId,
                UserId = _managerId,
                Role = TournamentMemberRole.Manager,
                Permissions = TournamentPermission.All,
                IsActive = false,
                RevokedAt = DateTime.UtcNow
            });

        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _managerId,
            TournamentPermission.ManageMatches);

        Assert.False(result);
    }

    [Fact]
    public async Task RevokedAt_SetWithoutIsActiveFalse_StillDenied()
    {
        _staff.Setup(x => x.GetActiveAsync(
                _tournamentId,
                _managerId))
            .ReturnsAsync(new TournamentStaffModel
            {
                TournamentId = _tournamentId,
                UserId = _managerId,
                Role = TournamentMemberRole.Manager,
                Permissions = TournamentPermission.All,
                IsActive = true,
                RevokedAt = DateTime.UtcNow
            });

        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _managerId,
            TournamentPermission.ManageMatches);

        Assert.False(result);
    }

    [Fact]
    public async Task NoPermissionsFlag_IsRejected()
    {
        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _organizerId,
            TournamentPermission.None);

        Assert.False(result);
    }

    [Fact]
    public async Task InvalidPermissionsFlag_IsRejected()
    {
        var invalidPermission = (TournamentPermission)1024;

        var result = await _service.HasPermissionAsync(
            _tournamentId,
            _organizerId,
            invalidPermission);

        Assert.False(result);
    }

    [Fact]
    public async Task EmptyUserId_IsRejected()
    {
        var result = await _service.HasPermissionAsync(
            _tournamentId,
            "",
            TournamentPermission.ManageMatches);

        Assert.False(result);
    }
}
