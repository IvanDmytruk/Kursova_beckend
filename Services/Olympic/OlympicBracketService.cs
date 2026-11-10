using MongoDB.Driver;
using Beckend.DTOs.Olympic;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Repositories;
using Beckend.Services.Olympic.Interfaces;
using Beckend.Services.TournamentGenerators;

namespace Beckend.Services.Olympic;

public class OlympicBracketService : IOlympicBracketService
{
    private const string MainStageId = "main";

    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly OlympicBracketRepository _brackets;
    private readonly OlympicGenerator _generator;
    private readonly IMongoClient _mongoClient;

    public OlympicBracketService(
        TournamentRepository tournaments,
        MatchRepository matches,
        OlympicBracketRepository brackets,
        OlympicGenerator generator,
        IMongoClient mongoClient)
    {
        _tournaments = tournaments;
        _matches = matches;
        _brackets = brackets;
        _generator = generator;
        _mongoClient = mongoClient;
    }


    public async Task<OlympicBracketResponseDto>
        GenerateFirstRoundAsync(string tournamentId)
    {
        var tournament = await GetTournamentAsync(tournamentId);

        if (tournament.TournamentType != TournamentType.OlympicSystem)
            throw new InvalidOperationException(
                "Tournament must use OlympicSystem.");

        if (tournament.Status != TournamentStatus.Ready)
            throw new InvalidOperationException(
                "First round requires Ready status.");

        var participants = tournament.TournamentParticipants;

        if (participants == null || participants.Count < 2)
            throw new InvalidOperationException(
                "At least 2 participants are required.");

        var existing = await _brackets.GetAsync(
            tournamentId, MainStageId);

        if (existing != null)
            throw new InvalidOperationException(
                "Olympic bracket already exists.");

        var existingMatches =
            await _matches.GetMatchesByTournamentIdAsync(tournamentId);

        if (existingMatches.Count > 0)
            throw new InvalidOperationException(
                "Tournament already contains matches.");

        var slots = _generator.CreateSlots(
            participants,
            tournament.Settings?.ShuffleParticipants ?? true);

        var generated = _generator.GenerateFirstRound(
            tournament, slots, MainStageId);

        var state = new OlympicBracketState
        {
            TournamentId = tournamentId,
            StageId = MainStageId,
            ParticipantCount = participants.Count,
            BracketSize = slots.Count,
            CurrentRound = 0,
            Slots = slots
        };


        state.CurrentRound = 1;

        using (var session = await _mongoClient.StartSessionAsync())
        {
            session.StartTransaction();

            try
            {

                await _brackets.CreateAsync(session, state);

                await _matches.CreateManyAsync(
                    session, generated);

                bool tournamentStarted =
                    await _tournaments.StartOlympicTournamentAsync(
                        session,
                        tournamentId);

                if (!tournamentStarted)
                {
                    throw new InvalidOperationException(
                        "Tournament could not be started. " +
                        "Expected Ready status.");
                }

                await session.CommitTransactionAsync();

            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync();

                throw;
            }
        }

        var byes = new List<string>();

        for (int i = 0; i < slots.Count; i += 2)
        {
            if (slots[i] != null && slots[i + 1] == null)
                byes.Add(slots[i]!);

            if (slots[i] == null && slots[i + 1] != null)
                byes.Add(slots[i + 1]!);
        }

        return BuildResponse(state, generated, byes);
    }

    public async Task<OlympicBracketResponseDto> GenerateNextRoundAsync(string tournamentId)
    {
        var tournament = await GetTournamentAsync(tournamentId);

        if (tournament.TournamentType != TournamentType.OlympicSystem)
            throw new InvalidOperationException(
                "Tournament must use OlympicSystem.");
        if (tournament.Status != TournamentStatus.InProgress &&
            tournament.Status != TournamentStatus.Completed)
        {
            throw new InvalidOperationException(
                "Next rounds require InProgress or Completed status.");
        }


        var state = await _brackets.GetAsync(
            tournamentId, MainStageId);

        if (state == null || state.CurrentRound < 1)
            throw new InvalidOperationException(
                "First round has not been generated.");

        if (state.IsCompleted)
        {
            if (string.IsNullOrWhiteSpace(state.ChampionTeamId))
                throw new InvalidOperationException(
                    "Completed bracket has no champion.");

            if (tournament.Status == TournamentStatus.InProgress)
            {
                throw new InvalidOperationException(
                    "Bracket is completed, but tournament is still " +
                    "InProgress. Reconciliation is required.");
            }

            if (tournament.Status != TournamentStatus.Completed)
            {
                throw new InvalidOperationException(
                    "Completed bracket has an invalid tournament status.");
            }

            return BuildResponse(
                state,
                new List<Match>(),
                new List<string>());
        }

        if (tournament.Status != TournamentStatus.InProgress)
        {
            throw new InvalidOperationException(
                "Cannot generate rounds for a completed tournament.");
        }


        if (state.BracketSize < 2 ||
            (state.BracketSize & (state.BracketSize - 1)) != 0 ||
            state.Slots.Count != state.BracketSize)
        {
            throw new InvalidOperationException(
                "Invalid saved Olympic bracket.");
        }

        int currentRound = state.CurrentRound;
        int totalRounds = GetTotalRounds(state.BracketSize);

        if (currentRound > totalRounds)
            throw new InvalidOperationException(
                "Invalid current round.");

        var allMatches =
            await _matches.GetMatchesByTournamentIdAsync(tournamentId);

        var currentMatches = allMatches
            .Where(m => m.StageId == state.StageId &&
                        m.RoundNumber == currentRound)
            .ToList();

        var nextMatchesAlreadyExist = allMatches.Any(m =>
            m.StageId == state.StageId &&
            m.RoundNumber == currentRound + 1);

        if (nextMatchesAlreadyExist)
            throw new InvalidOperationException(
                "Next round already contains matches.");

        var advancing = OlympicAdvancementResolver.Resolve(state, currentMatches, currentRound);

        if (currentRound == totalRounds)
        {
            if (advancing.Count != 1 ||
                advancing[0] == null)
            {
                throw new InvalidOperationException(
                    "Cannot determine the champion.");
            }

            long finalExpectedVersion = state.Version;

            state.ChampionTeamId = advancing[0];
            state.IsCompleted = true;

            using (var session = await _mongoClient.StartSessionAsync())
            {
                session.StartTransaction();

                try
                {
                    bool bracketUpdated =
                        await _brackets.TryUpdateAsync(
                            session,
                            state,
                            finalExpectedVersion);

                    if (!bracketUpdated)
                        throw new InvalidOperationException(
                            "Bracket was modified by another request.");

                    bool tournamentCompleted =
                        await _tournaments.CompleteOlympicTournamentAsync(
                            session,
                            tournamentId);

                    if (!tournamentCompleted)
                        throw new InvalidOperationException(
                            "Tournament could not be completed.");

                    await session.CommitTransactionAsync();

                    state.Version = finalExpectedVersion + 1;
                }
                catch
                {
                    if (session.IsInTransaction)
                        await session.AbortTransactionAsync();

                    throw;
                }
            }

            return BuildResponse(
                state,
                new List<Match>(),
                new List<string>());
        }

        if (advancing.Count % 2 != 0)
            throw new InvalidOperationException(
                "Invalid number of bracket positions.");

        int nextRound = currentRound + 1;
        var generated = new List<Match>();
        var automaticAdvancements = new List<string>();

        for (int i = 0; i < advancing.Count; i += 2)
        {
            string? home = advancing[i];
            string? away = advancing[i + 1];

            if (home == null && away == null)
                continue;

            if (home == null || away == null)
            {
                automaticAdvancements.Add(home ?? away!);
                continue;
            }

            if (home == away)
                throw new InvalidOperationException(
                    "A team cannot play against itself.");

            generated.Add(new Match
            {
                TournamentId = tournament.Id,
                StageId = state.StageId,
                MatchName =
                    $"{tournament.TournamentName} - " +
                    $"Olympic R{nextRound} M{i / 2 + 1}",
                HomeTeamId = home,
                AwayTeamId = away,
                SportName = tournament.SportName,
                RoundNumber = nextRound,
                BracketPosition = i / 2 + 1,
                IsAutomatic = true,
                Status = MatchStatus.Planing
            });
        }

        if (generated.Count == 0)
            throw new InvalidOperationException(
                "No matches could be generated for the next round.");


        long expectedVersion = state.Version;
        state.CurrentRound = nextRound;

        using (var session = await _mongoClient.StartSessionAsync())
        {
            session.StartTransaction();

            try
            {
                bool updated = await _brackets.TryUpdateAsync(
                    session, state, expectedVersion);

                if (!updated)
                    throw new InvalidOperationException(
                        "Bracket was modified by another request.");

                await _matches.CreateManyAsync(
                    session, generated);

                await session.CommitTransactionAsync();

                state.Version = expectedVersion + 1;
            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync();

                throw;
            }
        }
        return BuildResponse(
            state, generated, automaticAdvancements);
    }

    private static List<string?> ResolveAdvancingTeams(
        OlympicBracketState state,
        List<Match> matches,
        int round)
    {
        int positions = state.BracketSize >> round;

        if (positions < 1)
            throw new InvalidOperationException(
                "Invalid round number.");

        var winners = new string?[positions];
        var seenPositions = new HashSet<int>();

        foreach (var match in matches)
        {
            int index = match.BracketPosition - 1;

            if (index < 0 || index >= positions ||
                !seenPositions.Add(index))
            {
                throw new InvalidOperationException(
                    "Invalid or duplicate bracket position.");
            }

            if (match.Status != MatchStatus.Completed)
                throw new InvalidOperationException(
                    $"Match {match.Id} is not completed.");

            if (string.IsNullOrWhiteSpace(match.WinnerId) ||
                (match.WinnerId != match.HomeTeamId &&
                 match.WinnerId != match.AwayTeamId))
            {
                throw new InvalidOperationException(
                    $"Match {match.Id} has no valid winner.");
            }

            winners[index] = match.WinnerId;
        }

        if (round == 1)
        {
            for (int position = 0; position < positions; position++)
            {
                string? first = state.Slots[position * 2];
                string? second = state.Slots[position * 2 + 1];

                if (first == null && second == null)
                    throw new InvalidOperationException(
                        "Two empty slots in the same first-round pairing.");

                if (first != null && second != null)
                {
                    if (!seenPositions.Contains(position))
                        throw new InvalidOperationException(
                            $"Missing first-round match at position {position + 1}.");

                    var match = matches.Single(m =>
                        m.BracketPosition == position + 1);

                    bool sameParticipants =
                        (match.HomeTeamId == first &&
                         match.AwayTeamId == second) ||
                        (match.HomeTeamId == second &&
                         match.AwayTeamId == first);

                    if (!sameParticipants)
                        throw new InvalidOperationException(
                            "Match participants do not match saved bracket.");
                }
                else
                {
                    if (seenPositions.Contains(position))
                        throw new InvalidOperationException(
                            "A BYE position must not contain a match.");

                    winners[position] = first ?? second;
                }
            }
        }
        else
        {
            if (seenPositions.Count != positions)
                throw new InvalidOperationException(
                    "Some matches of the current round are missing.");
        }

        if (winners.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Not all advancing teams could be determined.");

        if (winners.Distinct().Count() != winners.Length)
            throw new InvalidOperationException(
                "Duplicate advancing teams detected.");

        return winners.ToList();
    }

    private static int GetTotalRounds(int bracketSize)
    {
        int rounds = 0;

        while (bracketSize > 1)
        {
            bracketSize /= 2;
            rounds++;
        }

        return rounds;
    }

    private async Task<Tournament> GetTournamentAsync(
        string tournamentId)
    {
        return await _tournaments.GetByIdAsync(tournamentId)
            ?? throw new KeyNotFoundException(
                "Tournament not found.");
    }

    private static OlympicBracketResponseDto BuildResponse(
        OlympicBracketState state,
        List<Match> matches,
        List<string> automaticAdvancements)
    {
        return new OlympicBracketResponseDto
        {
            TournamentId = state.TournamentId,
            StageId = state.StageId,
            ParticipantCount = state.ParticipantCount,
            BracketSize = state.BracketSize,
            RoundNumber = state.CurrentRound,
            ByesCount = automaticAdvancements.Count,
            MatchesCount = matches.Count,
            IsCompleted = state.IsCompleted,
            ChampionTeamId = state.ChampionTeamId,
            AutomaticAdvancements = automaticAdvancements,
            Matches = matches.Select(m => new OlympicMatchDto
            {
                Id = m.Id,
                HomeTeamId = m.HomeTeamId,
                AwayTeamId = m.AwayTeamId,
                RoundNumber = m.RoundNumber,
                BracketPosition = m.BracketPosition
            }).ToList()
        };
    }
}
