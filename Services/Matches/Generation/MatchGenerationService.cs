using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Tournaments;
using Beckend.Services.TournamentGenerators;
using MongoDB.Driver;
using Beckend.Repositories.TournamentGroups;

namespace Beckend.Services.Matches.Generation;

public class MatchGenerationService : IMatchGenerationService
{
    private readonly IMatchRepository _matchRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ISwissByeRepository _swissByeRepository;
    private readonly RoundRobinGenerator _roundRobinGenerator;
    private readonly FixedRoundsSwissGenerator _fixedSwissGenerator;
    private readonly ThresholdSwissGenerator _thresholdSwissGenerator;
    private readonly IMongoClient _mongoClient;
    private readonly IMapper _mapper;
    private readonly Cs2SwissGenerator _cs2SwissGenerator;
    private readonly GroupStageGenerator _groupStageGenerator;
    private readonly ITournamentGroupRepository _tournamentGroupRepository;
    private readonly DoubleEliminationGenerator _doubleEliminationGenerator;

    public MatchGenerationService(
        IMatchRepository matchRepository,
        ITournamentRepository tournamentRepository,
        ISwissByeRepository swissByeRepository,
        RoundRobinGenerator roundRobinGenerator,
        FixedRoundsSwissGenerator fixedSwissGenerator,
        ThresholdSwissGenerator thresholdSwissGenerator,
        IMongoClient mongoClient,
        IMapper mapper,
        Cs2SwissGenerator cs2SwissGenerator,
        GroupStageGenerator groupStageGenerator,
        ITournamentGroupRepository tournamentGroupRepository,
        DoubleEliminationGenerator doubleEliminationGenerator)
    {
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _swissByeRepository = swissByeRepository;
        _roundRobinGenerator = roundRobinGenerator;
        _fixedSwissGenerator = fixedSwissGenerator;
        _thresholdSwissGenerator = thresholdSwissGenerator;
        _mongoClient = mongoClient;
        _mapper = mapper;
        _cs2SwissGenerator = cs2SwissGenerator;
        _groupStageGenerator = groupStageGenerator;
        _tournamentGroupRepository = tournamentGroupRepository;
        _doubleEliminationGenerator = doubleEliminationGenerator;
    }

    public async Task<List<MatchDto>> GenerateRoundRobinAsync(
        string tournamentId)
    {
        var tournament = await _tournamentRepository
            .GetByIdAsync(tournamentId);

        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");

        if (tournament.TournamentType != TournamentType.RoundRobin)
        {
            throw new InvalidOperationException(
                "Tournament must use Round Robin format");
        }

        if (tournament.Status != TournamentStatus.Ready)
        {
            throw new InvalidOperationException(
                "Tournament must have Ready status");
        }

        if (tournament.TournamentParticipants.Count < 2)
        {
            throw new InvalidOperationException(
                "At least 2 participants are required");
        }

        var existingMatches = await _matchRepository
            .GetMatchesByTournamentIdAsync(tournamentId);

        if (existingMatches.Count > 0)
        {
            throw new InvalidOperationException(
                "Matches have already been created");
        }

        var matches = _roundRobinGenerator.Generate(tournament);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                "Round Robin generator returned no matches");
        }

        using var session = await _mongoClient.StartSessionAsync();

        session.StartTransaction();

        try
        {
            bool started = await _tournamentRepository
                .StartTournamentAsync(
                    session,
                    tournamentId,
                    TournamentType.RoundRobin);

            if (!started)
            {
                throw new InvalidOperationException(
                    "Tournament is no longer Ready");
            }

            await _matchRepository.CreateManyAsync(
                session,
                matches);

            await session.CommitTransactionAsync();
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }

        return _mapper.Map<List<MatchDto>>(matches);
    }

    public async Task<List<MatchDto>> GenerateSwissRoundAsync(string tournamentId)
    {
        using var session = await _mongoClient.StartSessionAsync();

        session.StartTransaction();

        try
        {
            var tournament = await _tournamentRepository.GetSwissTournamentAsync(session, tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            SwissRoundGenerationResult result;

            var existingMatches = await _matchRepository.GetSwissMatchesAsync(session, tournamentId);

            var previousByes = await _swissByeRepository.GetByTournamentAsync(session, tournamentId);

            int expectedRound = tournament.SwissLastGeneratedRound;
            bool firstRound = expectedRound == 0;

            if (firstRound &&
                tournament.Status != TournamentStatus.Ready)
            {
                throw new InvalidOperationException(
                    "First Swiss round requires Ready status");
            }

            if (!firstRound &&
                tournament.Status != TournamentStatus.InProgress)
            {
                throw new InvalidOperationException(
                    "Next Swiss round requires InProgress status");
            }

            int lastMatchRound = existingMatches.Count == 0
                ? 0
                : existingMatches.Max(m => m.RoundNumber);

            int lastByeRound = previousByes.Count == 0
                ? 0
                : previousByes.Max(b => b.RoundNumber);

            if (Math.Max(lastMatchRound, lastByeRound) != expectedRound)
            {
                throw new InvalidOperationException(
                    "Swiss round state is inconsistent");
            }

            var settings = tournament.Settings
                ?? new TournamentSettings();
            if (tournament.TournamentType == TournamentType.Cs2SwissSystem)
            {
                result = _cs2SwissGenerator.Generate(
                    tournament,
                    existingMatches,
                    previousByes);
            }
            else if (tournament.TournamentType == TournamentType.SwissSystem)
            {
                result = settings.SwissMode switch
                {
                    SwissMode.FixedRounds =>
                        _fixedSwissGenerator.Generate(
                            tournament,
                            existingMatches,
                            previousByes),

                    SwissMode.WinLossThreshold =>
                        _thresholdSwissGenerator.Generate(
                            tournament,
                            existingMatches,
                            previousByes),

                    _ => throw new NotSupportedException(
                        "Unsupported Swiss mode")
                };
            }
            else
            {
                throw new InvalidOperationException(
                    "Tournament is not a Swiss tournament");
            }


            if (result.Matches.Count == 0 && result.Bye == null)
            {
                throw new InvalidOperationException(
                    "Swiss generator returned an empty round");
            }

            int nextRound = expectedRound + 1;

            if (result.Matches.Any(
                    m => m.RoundNumber != nextRound) ||
                (result.Bye != null &&
                 result.Bye.RoundNumber != nextRound))
            {
                throw new InvalidOperationException(
                    "Generated Swiss round number is inconsistent");
            }

            bool advanced = firstRound
                ? await _tournamentRepository
                    .StartSwissTournamentAsync(
                        session,
                        tournamentId)
                : await _tournamentRepository
                    .AdvanceSwissRoundAsync(
                        session,
                        tournamentId,
                        expectedRound);

            if (!advanced)
            {
                throw new InvalidOperationException(
                    "Swiss round was already generated or tournament state changed");
            }

            if (result.Matches.Count > 0)
            {
                await _matchRepository.CreateManyAsync(
                    session,
                    result.Matches);
            }

            if (result.Bye != null)
            {
                await _swissByeRepository.CreateByeAsync(
                    session,
                    result.Bye);
            }

            if ((tournament.TournamentType == TournamentType.Cs2SwissSystem || (tournament.TournamentType == TournamentType.SwissSystem && settings.SwissMode == SwissMode.WinLossThreshold)) &&result.Matches.Count == 0 && result.Bye != null)
            {
                var records = tournament.TournamentParticipants
                    .ToDictionary(
                        t => t.Id,
                        t => (Wins: 0, Losses: 0));

                foreach (var match in existingMatches)
                {
                    if (!records.ContainsKey(match.HomeTeamId) ||
                        !records.ContainsKey(match.AwayTeamId) ||
                        match.HomeTeamId == match.AwayTeamId ||
                        match.Status != MatchStatus.Completed ||
                        !match.HomeScore.HasValue ||
                        !match.AwayScore.HasValue ||
                        match.HomeScore == match.AwayScore)
                    {
                        throw new InvalidOperationException(
                            "Invalid completed Threshold Swiss match");
                    }

                    string winnerId = match.HomeScore > match.AwayScore
                        ? match.HomeTeamId
                        : match.AwayTeamId;

                    string loserId = match.HomeScore > match.AwayScore
                        ? match.AwayTeamId
                        : match.HomeTeamId;

                    var winner = records[winnerId];
                    winner.Wins++;
                    records[winnerId] = winner;

                    var loser = records[loserId];
                    loser.Losses++;
                    records[loserId] = loser;
                }

                foreach (var bye in previousByes.Append(result.Bye))
                {
                    if (bye.TournamentId != tournament.Id ||
                        !records.ContainsKey(bye.TeamId))
                    {
                        throw new InvalidOperationException(
                            "Invalid Threshold Swiss BYE");
                    }

                    if (!bye.CountsAsWin)
                        continue;

                    var record = records[bye.TeamId];
                    record.Wins++;
                    records[bye.TeamId] = record;
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

            await session.CommitTransactionAsync();

            return _mapper.Map<List<MatchDto>>(result.Matches);
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }
    public async Task<List<MatchDto>> GenerateGroupStageAsync(string tournamentId)
    {
        using var session = await _mongoClient.StartSessionAsync();

        session.StartTransaction();

        try
        {
            var tournament = await _tournamentRepository
                .GetSwissTournamentAsync(session, tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournament.TournamentType != TournamentType.GroupStage)
                throw new InvalidOperationException(
                    "Tournament must use GroupStage format");

            if (tournament.Status != TournamentStatus.Ready)
                throw new InvalidOperationException(
                    "Tournament must have Ready status");

            var existingMatches = await _matchRepository
                .GetSwissMatchesAsync(session, tournamentId);

            if (existingMatches.Count > 0)
                throw new InvalidOperationException(
                    "Matches have already been generated");

            var result = _groupStageGenerator.Generate(tournament);

            if (result.Matches.Count == 0)
                throw new InvalidOperationException(
                    "GroupStage generator returned no matches");

            bool started = await _tournamentRepository
                .StartTournamentAsync(
                    session,
                    tournamentId,
                    TournamentType.GroupStage);

            if (!started)
                throw new InvalidOperationException(
                    "Tournament state changed during generation");

            await _matchRepository.CreateManyAsync(
                session,
                result.Matches);
            await _tournamentGroupRepository.CreateManyAsync(
                session,
                result.Groups);

            await session.CommitTransactionAsync();

            return _mapper.Map<List<MatchDto>>(result.Matches);
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }

    public async Task<List<MatchDto>> GenerateDoubleEliminationRoundAsync(
        string tournamentId)
    {
        using var session = await _mongoClient.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var tournament = await _tournamentRepository
                .GetSwissTournamentAsync(session, tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournament.TournamentType !=
                TournamentType.DoubleElimination)
            {
                throw new InvalidOperationException(
                    "Tournament must use DoubleElimination format");
            }

            int expectedRound =
                tournament.DoubleEliminationLastGeneratedRound;

            bool firstRound = expectedRound == 0;

            if (firstRound &&
                tournament.Status != TournamentStatus.Ready)
            {
                throw new InvalidOperationException(
                    "First round requires Ready status");
            }

            if (!firstRound &&
                tournament.Status != TournamentStatus.InProgress)
            {
                throw new InvalidOperationException(
                    "Next round requires InProgress status");
            }

            var allMatches = await _matchRepository
                .GetSwissMatchesAsync(session, tournamentId);

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
                .GetByTournamentAsync(session, tournamentId);

            int lastMatchRound = existingMatches.Count == 0
                ? 0
                : existingMatches.Max(m => m.RoundNumber);

            int lastByeRound = previousByes.Count == 0
                ? 0
                : previousByes.Max(b => b.RoundNumber);

            if (Math.Max(lastMatchRound, lastByeRound) != expectedRound)
            {
                throw new InvalidOperationException(
                    "DoubleElimination round state is inconsistent");
            }

            var result = _doubleEliminationGenerator.Generate(
                tournament,
                existingMatches,
                previousByes.Select(b => b.TeamId).ToList());

            if (result.IsCompleted)
            {
                throw new InvalidOperationException(
                    "Tournament already has a champion");
            }

            if (result.Matches.Count == 0)
            {
                throw new InvalidOperationException(
                    "Generator returned no matches");
            }

            int nextRound = expectedRound + 1;

            if (result.Matches.Any(m =>
                m.RoundNumber != nextRound))
            {
                throw new InvalidOperationException(
                    "Generated round number is inconsistent");
            }

            bool advanced = firstRound
                ? await _tournamentRepository
                    .StartDoubleEliminationAsync(
                        session, tournamentId)
                : await _tournamentRepository
                    .AdvanceDoubleEliminationRoundAsync(
                        session, tournamentId, expectedRound);

            if (!advanced)
            {
                throw new InvalidOperationException(
                    "Round already generated or tournament state changed");
            }

            await _matchRepository.CreateManyAsync(
                session, result.Matches);

            foreach (string teamId in result.ByeTeamIds)
            {
                await _swissByeRepository.CreateByeAsync(
                    session,
                    new SwissBye
                    {
                        TournamentId = tournamentId,
                        TeamId = teamId,
                        RoundNumber = nextRound,
                        PointsAwarded = 0,
                        CountsAsWin = false
                    });
            }

            await session.CommitTransactionAsync();

            return _mapper.Map<List<MatchDto>>(result.Matches);
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }

}