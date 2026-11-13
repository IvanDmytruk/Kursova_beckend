using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Repositories.Tournaments;

namespace Beckend.Services.Tournaments.Lifecycle;

public class TournamentLifecycleService
    : ITournamentLifecycleService
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMapper _mapper;

    public TournamentLifecycleService(
        ITournamentRepository tournamentRepository,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
    }
    public async Task<TournamentDto> ChangeStatusAsync(string tournamentId, TournamentStatus newStatus)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);

        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");


        if (tournament.Status == TournamentStatus.Ready &&
            newStatus == TournamentStatus.InProgress &&
            (tournament.TournamentType == TournamentType.RoundRobin ||
             tournament.TournamentType == TournamentType.SwissSystem))
        {
            throw new InvalidOperationException(
                "Tournament must be started by generating its matches.");
        }

        if (tournament.TournamentType == TournamentType.OlympicSystem &&
            (newStatus == TournamentStatus.InProgress ||
             newStatus == TournamentStatus.Completed))
        {
            throw new InvalidOperationException(
                "Olympic tournament status is managed automatically " +
                "by OlympicBracketService.");
        }

        var allowedTransitions =
            new Dictionary<TournamentStatus, TournamentStatus[]>
            {
                [TournamentStatus.Draft] =
                    new[] { TournamentStatus.Registration, TournamentStatus.Cancelled },

                [TournamentStatus.Registration] =
                    new[] { TournamentStatus.Ready, TournamentStatus.Cancelled },

                [TournamentStatus.Ready] =
                    new[] { TournamentStatus.InProgress, TournamentStatus.Cancelled },

                [TournamentStatus.InProgress] =
                    new[] { TournamentStatus.Completed, TournamentStatus.Cancelled },

                [TournamentStatus.Completed] =
                    Array.Empty<TournamentStatus>(),

                [TournamentStatus.Cancelled] =
                    Array.Empty<TournamentStatus>()
            };

        if (!allowedTransitions.TryGetValue(
                tournament.Status, out var allowed) ||
            !allowed.Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot change tournament status from {tournament.Status} to {newStatus}");
        }

        if (newStatus == TournamentStatus.Ready &&
            tournament.TournamentParticipants.Count < 2)
        {
            throw new InvalidOperationException(
                "At least 2 participants are required");
        }

        tournament.Status = newStatus;

        await _tournamentRepository.UpdateAsync(tournamentId, tournament);

        return _mapper.Map<TournamentDto>(tournament);
    }
}
