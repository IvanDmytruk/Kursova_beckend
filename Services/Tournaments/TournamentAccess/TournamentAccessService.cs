using Beckend.Enums;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.TournamentStaffs;

namespace Beckend.Services.TournamentAccess;

public class TournamentAccessService : ITournamentAccessService
{
    private readonly ITournamentRepository _tournaments;
    private readonly ITournamentStaffRepository _staff;

    public TournamentAccessService(
        ITournamentRepository tournaments,
        ITournamentStaffRepository staff)
    {
        _tournaments = tournaments;
        _staff = staff;
    }

    public async Task<bool> HasPermissionAsync(
        string tournamentId,
        string userId,
        TournamentPermission permission)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(tournamentId))
            return false;

        if (permission == TournamentPermission.None ||
            (permission & ~TournamentPermission.All) != 0)
            return false;

        var tournament = await _tournaments.GetByIdAsync(tournamentId);

        if (tournament == null)
            return false;

        if (tournament.CreatedBy == userId)
            return true;

        var manager = await _staff.GetActiveAsync(
            tournamentId, userId);

        if (manager == null ||!manager.IsActive || manager.RevokedAt != null || manager.Role != TournamentMemberRole.Manager)
        {
            return false;
        }

        return (manager.Permissions & permission) == permission;
    }
}
