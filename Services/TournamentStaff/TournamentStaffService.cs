using Beckend.Enums;
using Beckend.Repositories.Users;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.TournamentStaffs;
using Beckend.Services.TournamentAccess;

using StaffModel = Beckend.Models.TournamentStaff;

namespace Beckend.Services.TournamentStaff;

public class TournamentStaffService : ITournamentStaffService
{
    private readonly ITournamentStaffRepository _staff;
    private readonly ITournamentRepository _tournaments;
    private readonly IUserRepository _users;
    private readonly ITournamentAccessService _access;

    public TournamentStaffService( ITournamentStaffRepository staff,ITournamentRepository tournaments,IUserRepository users, ITournamentAccessService access)
    {
        _staff = staff;
        _tournaments = tournaments;
        _users = users;
        _access = access;
    }

    private static void ValidatePermissions(
        TournamentPermission permissions)
    {
        if ((permissions & ~TournamentPermission.All) != 0)
        {
            throw new ArgumentException(
                "Invalid tournament permissions.");
        }
    }

    private async Task EnsureOrganizerAsync(string tournamentId,string organizerId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new ArgumentException(
                "Tournament ID is required.");

        if (string.IsNullOrWhiteSpace(organizerId))
            throw new UnauthorizedAccessException(
                "Organizer ID is required.");

        var tournament = await _tournaments
            .GetByIdAsync(tournamentId)
            ?? throw new KeyNotFoundException(
                "Tournament not found.");

        if (tournament.CreatedBy != organizerId)
        {
            throw new UnauthorizedAccessException(
                "Only the tournament organizer can manage staff.");
        }

        var organizer = await _users.GetByIdAsync(organizerId);

        if (organizer == null)
            throw new UnauthorizedAccessException(
                "Organizer account not found.");

        if (IsBanned(organizer))
            throw new UnauthorizedAccessException(
                "Organizer account is banned.");
    }

    private static bool IsBanned(Beckend.Models.User user)
    {
        return user.IsBanned &&
            (user.BannedUntil == null ||
             user.BannedUntil > DateTime.UtcNow);
    }

    public async Task<StaffModel> AssignManagerAsync(string tournamentId, string userId,TournamentPermission permissions,string organizerId)
    {
        await EnsureOrganizerAsync(
            tournamentId, organizerId);

        ValidatePermissions(permissions);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException(
                "Manager user ID is required.");

        if (userId == organizerId)
            throw new InvalidOperationException(
                "Organizer cannot assign themselves as manager.");

        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException(
                "Manager user not found.");

        if (IsBanned(user))
            throw new InvalidOperationException(
                "Banned users cannot become managers.");

        var existing = await _staff.GetActiveAsync(
            tournamentId, userId);

        if (existing != null)
            throw new InvalidOperationException(
                "User is already an active manager.");

        var manager = new StaffModel
        {
            TournamentId = tournamentId,
            UserId = userId,
            Role = TournamentMemberRole.Manager,
            Permissions = permissions,
            AssignedBy = organizerId,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        };

        return await _staff.AssignAsync(manager);
    }

    public async Task<bool> UpdateManagerPermissionsAsync(string tournamentId,string managerId,TournamentPermission permissions, string organizerId)
    {
        await EnsureOrganizerAsync(
            tournamentId, organizerId);

        ValidatePermissions(permissions);

        if (string.IsNullOrWhiteSpace(managerId))
            throw new ArgumentException(
                "Manager ID is required.");

        var manager = await _staff.GetActiveAsync(
            tournamentId, managerId);

        if (manager == null)
            throw new KeyNotFoundException(
                "Active manager not found.");

        return await _staff.UpdatePermissionsAsync(
            tournamentId,
            managerId,
            permissions);
    }

    public async Task<bool> RevokeManagerAsync( string tournamentId,string managerId,string organizerId)
    {
        await EnsureOrganizerAsync(
            tournamentId, organizerId);

        if (string.IsNullOrWhiteSpace(managerId))
            throw new ArgumentException(
                "Manager ID is required.");

        var manager = await _staff.GetActiveAsync(
            tournamentId, managerId);

        if (manager == null)
            throw new KeyNotFoundException(
                "Active manager not found.");

        return await _staff.RevokeAsync(
            tournamentId,
            managerId);
    }


    public async Task<List<StaffModel>> GetManagersAsync( string tournamentId,string requesterId)
    {
        if (string.IsNullOrWhiteSpace(requesterId))
            throw new UnauthorizedAccessException(
                "Authentication is required.");

        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new ArgumentException(
                "Tournament ID is required.");

        var requester = await _users.GetByIdAsync(requesterId);

        if (requester == null || IsBanned(requester))
            throw new UnauthorizedAccessException(
                "Requester is not allowed.");

        var allowed = await _access.HasPermissionAsync(
            tournamentId,
            requesterId,
            TournamentPermission.ViewStaff);

        if (!allowed)
            throw new UnauthorizedAccessException(
                "You cannot view tournament staff.");

        return await _staff.GetByTournamentAsync(tournamentId);
    }

}
