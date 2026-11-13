using Beckend.DTOs;
using Beckend.Enums;

namespace Beckend.Services.Tournaments.Lifecycle;

public interface ITournamentLifecycleService
{
    Task<TournamentDto> ChangeStatusAsync(
        string tournamentId,
        TournamentStatus newStatus);
}
