using AutoMapper;
using Beckend.DTOs;
using Beckend.DTOs.Match;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.TournamentAdministration;
using Beckend.Repositories.Tournaments;
using Beckend.Services.TournamentGenerators;
using MongoDB.Driver;

namespace Beckend.Services.Matches.Results;

public class MatchResultService : IMatchResultService
{
    private readonly IMatchRepository _matchRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ISwissByeRepository _swissByeRepository;
    private readonly ITournamentAdministrationRepository _administration;
    private readonly IMongoClient _mongoClient;
    private readonly IMapper _mapper;
    private readonly DoubleEliminationGenerator _doubleEliminationGenerator;

    public MatchResultService(
        IMatchRepository matchRepository,
        ITournamentRepository tournamentRepository,
        ISwissByeRepository swissByeRepository,
        ITournamentAdministrationRepository administration,
        IMongoClient mongoClient,
        IMapper mapper,
        DoubleEliminationGenerator doubleEliminationGenerator)
    {
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _swissByeRepository = swissByeRepository;
        _administration = administration;
        _mongoClient = mongoClient;
        _mapper = mapper;
        _doubleEliminationGenerator = doubleEliminationGenerator;
    }
    public Task<MatchDto> UpdateResultAsync(
        string matchId,
        UpdateMatchResultDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return CompleteResultAsync(
            matchId,
            dto.HomeScore,
            dto.AwayScore,
            null,
            null);
    }
    public Task<MatchDto> SetAdministrativeResultAsync(
        string matchId,
        AdminMatchResultDto dto,
        string administratorId)
    {
        ValidateAdministrativeRequest(dto, administratorId);

        return CompleteResultAsync(
            matchId,
            dto.HomeScore,
            dto.AwayScore,
            dto,
            administratorId);
    }
    public Task<MatchDto> SetAdministrativeResultAsync(
        IClientSessionHandle session,
        string matchId,
        AdminMatchResultDto dto,
        string administratorId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsInTransaction)
        {
            throw new InvalidOperationException(
                "An active MongoDB transaction is required.");
        }

        ValidateAdministrativeRequest(dto, administratorId);

        return CompleteResultCoreAsync(
            session,
            matchId,
            dto.HomeScore,
            dto.AwayScore,
            dto,
            administratorId);
    }

    private static void ValidateAdministrativeRequest(
        AdminMatchResultDto dto,
        string administratorId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.ResultType == MatchResultType.Normal ||
            !Enum.IsDefined(dto.ResultType))
        {
            throw new ArgumentException(
                "Invalid administrative result type.");
        }

        if (string.IsNullOrWhiteSpace(administratorId))
        {
            throw new ArgumentException(
                "Administrator ID is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason) ||
            dto.Reason.Trim().Length < 5)
        {
            throw new ArgumentException(
                "Reason must contain at least 5 characters.");
        }
    }
    private async Task<MatchDto> CompleteResultAsync(
        string matchId,
        int? requestedHomeScore,
        int? requestedAwayScore,
        AdminMatchResultDto? adminDto,
        string? administratorId)
    {
        using var session = await _mongoClient.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var result = await CompleteResultCoreAsync(
                session,
                matchId,
                requestedHomeScore,
                requestedAwayScore,
                adminDto,
                administratorId);

            await session.CommitTransactionAsync();

            return result;
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }
    private async Task<MatchDto> CompleteResultCoreAsync(
    IClientSessionHandle session,
    string matchId,
    int? requestedHomeScore,
    int? requestedAwayScore,
    AdminMatchResultDto? adminDto,
    string? administratorId,
    bool deferTournamentCompletion = false)
    {
        if (!session.IsInTransaction)
        {
            throw new InvalidOperationException(
                "An active MongoDB transaction is required.");
        }

        var match = await _matchRepository
            .GetByIdAsync(session, matchId)
            ?? throw new KeyNotFoundException(
                "Match not found.");

        var tournament = await _tournamentRepository
            .GetByIdAsync(session, match.TournamentId)
            ?? throw new KeyNotFoundException(
                "Tournament not found.");

        if (tournament.Status != TournamentStatus.InProgress)
        {
            throw new InvalidOperationException(
                "Tournament must be InProgress.");
        }

        if (match.Status == MatchStatus.Completed ||
            match.Status == MatchStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cannot update a completed or cancelled match.");
        }

        if (!requestedHomeScore.HasValue ||
            !requestedAwayScore.HasValue)
        {
            throw new ArgumentException(
                "Both scores are required.");
        }

        int homeScore = requestedHomeScore.Value;
        int awayScore = requestedAwayScore.Value;

        if (homeScore < 0 || awayScore < 0)
        {
            throw new ArgumentException(
                "Scores cannot be negative.");
        }

        bool isOlympicMatch =
            tournament.TournamentType == TournamentType.OlympicSystem &&
            match.IsAutomatic &&
            match.StageId == "main";

        bool isThresholdSwiss =
            (tournament.TournamentType == TournamentType.SwissSystem &&
             tournament.Settings?.SwissMode ==
                 SwissMode.WinLossThreshold) ||
            tournament.TournamentType ==
                TournamentType.Cs2SwissSystem;

        bool isDoubleElimination =
            tournament.TournamentType ==
                TournamentType.DoubleElimination &&
            match.IsAutomatic &&
            (match.StageId == "double-main" ||
             match.StageId == "grand-final");

        if (homeScore == awayScore &&
            (isOlympicMatch ||
             isThresholdSwiss ||
             isDoubleElimination ||
             tournament.Settings?.AllowDraws != true))
        {
            throw new InvalidOperationException(
                "Draws are not allowed for this match.");
        }

        string? winnerId = homeScore > awayScore
            ? match.HomeTeamId
            : awayScore > homeScore
                ? match.AwayTeamId
                : null;
        var participantStates = await _administration
            .GetParticipantStatesAsync(
                session,
                tournament.Id);

        var disqualifiedTeamIds = participantStates
            .Where(s =>
                s.Status ==
                    TournamentParticipantStatus.Disqualified)
            .Select(s => s.TeamId)
            .ToHashSet();

        if (winnerId != null &&
            disqualifiedTeamIds.Contains(winnerId))
        {
            throw new InvalidOperationException(
                "Disqualified team cannot win a match.");
        }

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;
        match.WinnerId = winnerId;
        match.Status = MatchStatus.Completed;

        if (adminDto != null)
        {
            if (match.WinnerId != adminDto.WinnerTeamId)
            {
                throw new ArgumentException(
                    "WinnerTeamId does not match the score.");
            }

            match.ResultType = adminDto.ResultType;
            match.ResultReason = adminDto.Reason.Trim();
            match.ResultUpdatedBy = administratorId;
            match.ResultUpdatedAt = DateTime.UtcNow;
        }
        else
        {
            match.ResultType = MatchResultType.Normal;
            match.ResultReason = null;
            match.ResultUpdatedBy = null;
            match.ResultUpdatedAt = null;
        }

        bool updated = await _matchRepository
     .CompleteMatchAsync(session, match);

        if (!updated)
        {
            throw new InvalidOperationException(
                "Match result has already been updated.");
        }

        if (!deferTournamentCompletion)
        {
            await FinalizeTournamentIfReadyAsync(
                session,
                tournament.Id);
        }

        return _mapper.Map<MatchDto>(match);
    }
    public async Task FinalizeTournamentIfReadyAsync(
    IClientSessionHandle session,
    string tournamentId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsInTransaction)
            throw new InvalidOperationException(
                "An active MongoDB transaction is required.");

        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new ArgumentException(
                "Tournament ID is required.", nameof(tournamentId));

        var tournament = await _tournamentRepository
            .GetByIdAsync(session, tournamentId)
            ?? throw new KeyNotFoundException(
                "Tournament not found.");

        if (tournament.Status != TournamentStatus.InProgress)
            throw new InvalidOperationException(
                "Tournament must be InProgress.");

        var participantStates = await _administration
            .GetParticipantStatesAsync(session, tournamentId);

        var disqualifiedTeamIds = participantStates
            .Where(s =>
                s.Status == TournamentParticipantStatus.Disqualified)
            .Select(s => s.TeamId)
            .ToHashSet();

        bool hasUnfinished = await _matchRepository
            .HasUnfinishedMatchesAsync(session, tournamentId);

        if (hasUnfinished)
            return;

        switch (tournament.TournamentType)
        {
            case TournamentType.RoundRobin:
                {
                    bool completed = await _tournamentRepository
                        .CompleteRoundRobinAsync(session, tournamentId);

                    if (!completed)
                        throw new InvalidOperationException(
                            "Could not complete Round Robin tournament.");

                    break;
                }

            case TournamentType.SwissSystem:
                {
                    if (tournament.Settings?.SwissMode ==
                        SwissMode.FixedRounds)
                    {
                        int finalRound = tournament.Settings.RoundsCount;

                        if (finalRound > 0 &&
                            tournament.SwissLastGeneratedRound == finalRound)
                        {
                            bool completed = await _tournamentRepository
                                .CompleteFixedRoundsSwissAsync(
                                    session, tournamentId, finalRound);

                            if (!completed)
                                throw new InvalidOperationException(
                                    "Could not complete FixedRounds Swiss tournament.");
                        }
                    }
                    else if (tournament.Settings?.SwissMode ==
                             SwissMode.WinLossThreshold)
                    {
                        await FinalizeThresholdSwissAsync(
                            session,
                            tournament,
                            disqualifiedTeamIds);
                    }

                    break;
                }

            case TournamentType.Cs2SwissSystem:
                {
                    await FinalizeThresholdSwissAsync(
                        session,
                        tournament,
                        disqualifiedTeamIds);

                    break;
                }

            case TournamentType.DoubleElimination:
                {
                    await FinalizeDoubleEliminationAsync(
                        session,
                        tournament,
                        disqualifiedTeamIds);

                    break;
                }
            case TournamentType.OlympicSystem:
            case TournamentType.GroupStage:
                break;

            default:
                throw new InvalidOperationException(
                    "Unsupported tournament format.");
        }
    }
    public async Task<List<MatchDto>> SetAdministrativeResultsBatchAsync(
        IClientSessionHandle session,
        IReadOnlyCollection<AdministrativeMatchResultRequest> requests,
        string administratorId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(requests);

        if (!session.IsInTransaction)
            throw new InvalidOperationException(
                "An active MongoDB transaction is required.");

        if (string.IsNullOrWhiteSpace(administratorId))
            throw new ArgumentException(
                "Administrator ID is required.");

        if (requests.Any(x => x == null))
            throw new ArgumentException(
                "Batch contains a null request.");

        var items = requests.ToList();

        if (items.Any(x => string.IsNullOrWhiteSpace(x.MatchId)))
            throw new ArgumentException(
                "Every request must contain MatchId.");

        if (items.Select(x => x.MatchId).Distinct().Count() != items.Count)
            throw new ArgumentException(
                "Batch contains duplicate match IDs.");

        var results = new List<MatchDto>();

        foreach (var request in items)
        {
            ValidateAdministrativeRequest(
                request.Result,
                administratorId);

            var result = await CompleteResultCoreAsync(
                session,
                request.MatchId,
                request.Result.HomeScore,
                request.Result.AwayScore,
                request.Result,
                administratorId,
                deferTournamentCompletion: true);

            results.Add(result);
        }

        return results;
    }

    private async Task FinalizeThresholdSwissAsync(
        IClientSessionHandle session,
        Tournament tournament,
        HashSet<string> disqualifiedTeamIds)
    {
        var settings = tournament.Settings
            ?? throw new InvalidOperationException(
                "Swiss settings are missing.");

        if (settings.WinsToAdvance <= 0 ||
            settings.LossesToEliminate <= 0)
            throw new InvalidOperationException(
                "Invalid Swiss thresholds.");

        var allMatches = await _matchRepository
            .GetSwissMatchesAsync(session, tournament.Id);
        if (allMatches.Any(m => m.Status == MatchStatus.Cancelled))
            throw new InvalidOperationException(
                "Cancelled Swiss matches require an administrative decision.");

        var byes = await _swissByeRepository
            .GetByTournamentAsync(session, tournament.Id);

        var records = tournament.TournamentParticipants
            .ToDictionary(
                t => t.Id,
                t => (Wins: 0, Losses: 0));

        foreach (var match in allMatches)
        {
            if (match.Status != MatchStatus.Completed ||
                !match.HomeScore.HasValue ||
                !match.AwayScore.HasValue)
                throw new InvalidOperationException(
                    "Swiss match is not completed.");

            if (match.HomeScore.Value == match.AwayScore.Value)
                throw new InvalidOperationException(
                    "Swiss matches cannot end in a draw.");

            if (!records.ContainsKey(match.HomeTeamId) ||
                !records.ContainsKey(match.AwayTeamId) ||
                match.HomeTeamId == match.AwayTeamId)
                throw new InvalidOperationException(
                    "Swiss match contains invalid participants.");

            string winnerId =
                match.HomeScore.Value > match.AwayScore.Value
                    ? match.HomeTeamId
                    : match.AwayTeamId;

            string loserId =
                winnerId == match.HomeTeamId
                    ? match.AwayTeamId
                    : match.HomeTeamId;

            var winner = records[winnerId];
            winner.Wins++;
            records[winnerId] = winner;

            var loser = records[loserId];
            loser.Losses++;
            records[loserId] = loser;
        }

        foreach (var bye in byes)
        {
            if (bye.TournamentId != tournament.Id ||
                !records.ContainsKey(bye.TeamId))
                throw new InvalidOperationException(
                    "Swiss BYE contains invalid participant.");

            if (!bye.CountsAsWin)
                continue;

            var record = records[bye.TeamId];
            record.Wins++;
            records[bye.TeamId] = record;
        }

        bool allDecided = records.All(r =>
            disqualifiedTeamIds.Contains(r.Key) ||
            r.Value.Wins >= settings.WinsToAdvance ||
            r.Value.Losses >= settings.LossesToEliminate);

        if (!allDecided)
            return;

        bool completed;

        if (tournament.TournamentType ==
            TournamentType.Cs2SwissSystem)
        {
            completed = await _tournamentRepository
                .CompleteCs2SwissAsync(session, tournament.Id);
        }
        else
        {
            completed = await _tournamentRepository
                .CompleteThresholdSwissAsync(session, tournament.Id);
        }

        if (!completed)
            throw new InvalidOperationException(
                "Could not complete Swiss tournament.");
    }

    private async Task FinalizeDoubleEliminationAsync(
        IClientSessionHandle session,
        Tournament tournament,
        HashSet<string> disqualifiedTeamIds)
    {
        var allMatches = await _matchRepository
            .GetSwissMatchesAsync(session, tournament.Id);

        var existingMatches = allMatches
            .Where(m => m.IsAutomatic &&
                (m.StageId == "double-main" ||
                 m.StageId == "grand-final"))
            .ToList();

        if (allMatches.Count != existingMatches.Count)
            throw new InvalidOperationException(
                "Tournament contains matches outside DoubleElimination.");

        if (existingMatches.Any(m =>
            m.Status == MatchStatus.Cancelled))
            throw new InvalidOperationException(
                "Cancelled elimination matches require an administrative decision.");

        var previousByes = await _swissByeRepository
            .GetByTournamentAsync(session, tournament.Id);

        var result = _doubleEliminationGenerator.Generate(
            tournament,
            existingMatches,
            previousByes.Select(b => b.TeamId).ToList(),
            disqualifiedTeamIds);

        if (!result.IsCompleted)
            return;

        if (string.IsNullOrWhiteSpace(result.ChampionId) ||
            disqualifiedTeamIds.Contains(result.ChampionId))
            throw new InvalidOperationException(
                "Invalid DoubleElimination champion.");

        bool completed = await _tournamentRepository
            .CompleteDoubleEliminationAsync(
                session,
                tournament.Id,
                result.ChampionId,
                tournament.DoubleEliminationLastGeneratedRound);

        if (!completed)
            throw new InvalidOperationException(
                "Could not complete DoubleElimination tournament.");
    }

}
