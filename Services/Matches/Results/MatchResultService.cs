using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Tournaments;
using MongoDB.Driver;
using Beckend.Services.TournamentGenerators;

namespace Beckend.Services.Matches.Results;

public class MatchResultService : IMatchResultService
{
    private readonly IMatchRepository _matchRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ISwissByeRepository _swissByeRepository;
    private readonly IMongoClient _mongoClient;
    private readonly IMapper _mapper;
    private readonly DoubleEliminationGenerator _doubleEliminationGenerator;


    public MatchResultService(
        IMatchRepository matchRepository,
        ITournamentRepository tournamentRepository,
        ISwissByeRepository swissByeRepository,
        IMongoClient mongoClient,
        IMapper mapper,
        DoubleEliminationGenerator doubleEliminationGenerator)
    {
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _swissByeRepository = swissByeRepository;
        _mongoClient = mongoClient;
        _mapper = mapper;
        _doubleEliminationGenerator = doubleEliminationGenerator;
    }

    public async Task<MatchDto> UpdateResultAsync(
        string matchId,
        UpdateMatchResultDto dto)
    {
        var match = await _matchRepository.GetByIdAsync(matchId);

        if (match == null)
            throw new KeyNotFoundException("Match not found");

        var tournament = await _tournamentRepository
            .GetByIdAsync(match.TournamentId);

        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");

        if (tournament.Status != TournamentStatus.InProgress)
        {
            throw new InvalidOperationException(
                "Tournament must be InProgress");
        }

        if (match.Status == MatchStatus.Completed ||
            match.Status == MatchStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cannot update a completed or cancelled match");
        }

        if (!dto.HomeScore.HasValue || !dto.AwayScore.HasValue)
        {
            throw new ArgumentException(
                "Both scores are required");
        }

        int homeScore = dto.HomeScore.Value;
        int awayScore = dto.AwayScore.Value;

        if (homeScore < 0 || awayScore < 0)
        {
            throw new ArgumentException(
                "Scores cannot be negative");
        }

        bool isOlympicMatch =
            tournament.TournamentType == TournamentType.OlympicSystem &&
            match.IsAutomatic &&
            match.StageId == "main";


        bool isThresholdSwiss =
            (tournament.TournamentType == TournamentType.SwissSystem &&
             tournament.Settings?.SwissMode == SwissMode.WinLossThreshold) ||
             tournament.TournamentType == TournamentType.Cs2SwissSystem;
        
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

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;

        match.WinnerId = homeScore > awayScore
            ? match.HomeTeamId
            : awayScore > homeScore
                ? match.AwayTeamId
                : null;

        match.Status = MatchStatus.Completed;


        using var session = await _mongoClient.StartSessionAsync();

        session.StartTransaction();

        try
        {
            bool updated = await _matchRepository
                .CompleteMatchAsync(session, match);

            if (!updated)
            {
                throw new InvalidOperationException(
                    "Match result has already been updated.");
            }

            // ROUND ROBIN
            if (tournament.TournamentType ==
                TournamentType.RoundRobin)
            {
                bool hasUnfinished = await _matchRepository
                    .HasUnfinishedMatchesAsync(
                        session,
                        tournament.Id);

                if (!hasUnfinished)
                {
                    bool completed = await _tournamentRepository
                        .CompleteRoundRobinAsync(
                            session,
                            tournament.Id);

                    if (!completed)
                    {
                        throw new InvalidOperationException(
                            "Could not complete Round Robin tournament.");
                    }
                }
            }

            // SWISS FIXED ROUNDS
            if (tournament.TournamentType ==
                    TournamentType.SwissSystem &&
                tournament.Settings?.SwissMode ==
                    SwissMode.FixedRounds)
            {
                bool hasUnfinished = await _matchRepository
                    .HasUnfinishedMatchesAsync(
                        session,
                        tournament.Id);

                if (!hasUnfinished)
                {
                    int finalRound =
                        tournament.Settings.RoundsCount;

                    if (finalRound > 0 &&
                        tournament.SwissLastGeneratedRound ==
                            finalRound)
                    {
                        bool completed =
                            await _tournamentRepository
                                .CompleteFixedRoundsSwissAsync(
                                    session,
                                    tournament.Id,
                                    finalRound);

                        if (!completed)
                        {
                            throw new InvalidOperationException(
                                "Could not complete FixedRounds Swiss tournament.");
                        }
                    }
                }
            }

            // SWISS WIN/LOSS THRESHOLD
            if (isThresholdSwiss)
            {
                bool hasUnfinished = await _matchRepository
                    .HasUnfinishedMatchesAsync(
                        session,
                        tournament.Id);

                if (!hasUnfinished)
                {
                    var swissMatches = await _matchRepository
                        .GetSwissMatchesAsync(
                            session,
                            tournament.Id);

                    var swissByes = await _swissByeRepository
                        .GetByTournamentAsync(
                            session,
                            tournament.Id);

                    var settings = tournament.Settings!;

                    var records =
                        tournament.TournamentParticipants
                            .ToDictionary(
                                t => t.Id,
                                t => (Wins: 0, Losses: 0));

                    foreach (var swissMatch in swissMatches)
                    {
                        if (!records.ContainsKey(
                                swissMatch.HomeTeamId) ||
                            !records.ContainsKey(
                                swissMatch.AwayTeamId) ||
                            swissMatch.HomeTeamId ==
                                swissMatch.AwayTeamId)
                        {
                            throw new InvalidOperationException(
                                "Swiss match contains invalid participants");
                        }

                        if (swissMatch.Status !=
                                MatchStatus.Completed ||
                            !swissMatch.HomeScore.HasValue ||
                            !swissMatch.AwayScore.HasValue)
                        {
                            throw new InvalidOperationException(
                                "Swiss match is not completed");
                        }

                        if (swissMatch.HomeScore.Value ==
                            swissMatch.AwayScore.Value)
                        {
                            throw new InvalidOperationException(
                                "Threshold Swiss cannot contain draws");
                        }

                        bool homeWon =
                            swissMatch.HomeScore.Value >
                            swissMatch.AwayScore.Value;

                        string winnerId = homeWon
                            ? swissMatch.HomeTeamId
                            : swissMatch.AwayTeamId;

                        string loserId = homeWon
                            ? swissMatch.AwayTeamId
                            : swissMatch.HomeTeamId;

                        var winner = records[winnerId];
                        winner.Wins++;
                        records[winnerId] = winner;

                        var loser = records[loserId];
                        loser.Losses++;
                        records[loserId] = loser;
                    }

                    foreach (var bye in swissByes)
                    {
                        if (bye.TournamentId !=
                                tournament.Id ||
                            !records.ContainsKey(
                                bye.TeamId))
                        {
                            throw new InvalidOperationException(
                                "Swiss BYE contains invalid participant");
                        }

                        if (bye.CountsAsWin)
                        {
                            var record = records[bye.TeamId];
                            record.Wins++;
                            records[bye.TeamId] = record;
                        }
                    }

                    bool allDecided = records.Values.All(r =>
                        r.Wins >= settings.WinsToAdvance ||
                        r.Losses >= settings.LossesToEliminate);

                    if (allDecided)
                    {
                        bool completed;

                        if (tournament.TournamentType ==
                            TournamentType.Cs2SwissSystem)
                        {
                            completed = await _tournamentRepository
                                .CompleteCs2SwissAsync(
                                    session,
                                    tournament.Id);
                        }
                        else
                        {
                            completed = await _tournamentRepository
                                .CompleteThresholdSwissAsync(
                                    session,
                                    tournament.Id);
                        }

                        if (!completed)
                        {
                            throw new InvalidOperationException(
                                "Could not complete Swiss tournament.");
                        }
                    }

                }
            }

            if (isDoubleElimination)
            {
                bool hasUnfinished = await _matchRepository
                    .HasUnfinishedMatchesAsync(
                        session, tournament.Id);

                if (!hasUnfinished)
                {
                    var allMatches = await _matchRepository
                        .GetSwissMatchesAsync(
                            session, tournament.Id);

                    var existingMatches = allMatches
                        .Where(m => m.IsAutomatic &&
                            (m.StageId == "double-main" ||
                             m.StageId == "grand-final"))
                        .ToList();

                    if (allMatches.Count != existingMatches.Count)
                    {
                        throw new InvalidOperationException(
                            "Tournament contains matches outside DoubleElimination");
                    }

                    var previousByes = await _swissByeRepository
                        .GetByTournamentAsync(
                            session, tournament.Id);

                    var result = _doubleEliminationGenerator.Generate(
                        tournament,
                        existingMatches,
                        previousByes.Select(b => b.TeamId).ToList());

                    if (result.IsCompleted)
                    {
                        bool completed = await _tournamentRepository
                            .CompleteDoubleEliminationAsync(
                                session,
                                tournament.Id,
                                result.ChampionId!,
                                tournament.DoubleEliminationLastGeneratedRound);

                        if (!completed)
                        {
                            throw new InvalidOperationException(
                                "Could not complete DoubleElimination tournament");
                        }
                    }
                }
            }

            await session.CommitTransactionAsync();
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }

        return _mapper.Map<MatchDto>(match);
    }
}