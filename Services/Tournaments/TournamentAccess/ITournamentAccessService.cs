using Beckend.Enums;

namespace Beckend.Services.TournamentAccess;

public interface ITournamentAccessService
{
    Task<bool> HasPermissionAsync(string tournamentId, string userId,TournamentPermission permission);
}