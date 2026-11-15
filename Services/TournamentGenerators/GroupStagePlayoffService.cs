
using Beckend.DTOs.Olympic;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Olympic;
using Beckend.Repositories.TournamentAdministration;
using Beckend.Repositories.TournamentGroups;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Olympic;
using Beckend.Services.TournamentStandings;
using MongoDB.Driver;

namespace Beckend.Services.TournamentGenerators;

public class GroupStagePlayoffService
{
    private const string GroupStageId = "groups";
    private const string PlayoffStageId = "playoff";

    private readonly ITournamentRepository _tournaments;
    private readonly IMatchRepository _matches;
    private readonly ITournamentGroupRepository _groups;
    private readonly IOlympicBracketRepository _brackets;
    private readonly GroupStageStandingsGenerator _standings;
    private readonly OlympicGenerator _olympic;
    private readonly IMongoClient _mongoClient;
    private readonly ITournamentAdministrationRepository _administration;

    public GroupStagePlayoffService(
        ITournamentRepository tournaments,
        IMatchRepository matches,
        ITournamentGroupRepository groups,
        IOlympicBracketRepository brackets,
        GroupStageStandingsGenerator standings,
        OlympicGenerator olympic,
        IMongoClient mongoClient,
        ITournamentAdministrationRepository administration)
    {
        _tournaments = tournaments;
        _matches = matches;
        _groups = groups;
        _brackets = brackets;
        _standings = standings;
        _olympic = olympic;
        _mongoClient = mongoClient;
        _administration = administration;
    }

    public async Task<OlympicBracketResponseDto> GenerateAsync(
        string tournamentId)
    {
        using var session = await _mongoClient.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var tournament = await _tournaments
                .GetSwissTournamentAsync(session, tournamentId)
                ?? throw new KeyNotFoundException(
                    "Tournament not found.");

            if (tournament.TournamentType != TournamentType.GroupStage)
                throw new InvalidOperationException(
                    "Tournament must use GroupStage.");

            if (tournament.Status != TournamentStatus.InProgress)
                throw new InvalidOperationException(
                    "Tournament must be InProgress.");

            var existingBracket = await _brackets.GetAsync(
                session, tournamentId, PlayoffStageId);

            if (existingBracket != null)
                throw new InvalidOperationException(
                    "Playoff bracket already exists.");

            var allMatches = await _matches
                .GetSwissMatchesAsync(session, tournamentId);

            if (allMatches.Count == 0 ||
                allMatches.Any(m => m.StageId != GroupStageId))
            {
                throw new InvalidOperationException(
                    "Tournament must contain only group matches.");
            }

            if (allMatches.Any(m =>
                m.Status != MatchStatus.Completed ||
                !m.HomeScore.HasValue ||
                !m.AwayScore.HasValue))
            {
                throw new InvalidOperationException(
                    "All group matches must be completed.");
            }

            var groups = await _groups
                .GetByTournamentIdAsync(session, tournamentId);

            if (groups.Count == 0)
                throw new InvalidOperationException(
                    "Tournament groups not found.");

            var participantStates = await _administration
                .GetParticipantStatesAsync(session, tournamentId);

            int matchesPerPair = tournament.Settings.MatchesPerPair;

            if (matchesPerPair < 1)
                throw new InvalidOperationException(
                    "Invalid MatchesPerPair.");

            foreach (var group in groups)
            {
                int teamCount = group.TeamIds.Count;

                int expectedMatches =
                    teamCount * (teamCount - 1) / 2 * matchesPerPair;

                var groupMatches = allMatches
                    .Where(m => m.GroupId == group.GroupId)
                    .ToList();

                if (groupMatches.Count != expectedMatches)
                {
                    throw new InvalidOperationException(
                        $"Incomplete match calendar for group {group.GroupId}. " +
                        $"Expected {expectedMatches}, found {groupMatches.Count}.");
                }

                var teamIds = group.TeamIds.ToHashSet();

                if (groupMatches.Any(m =>
                    !teamIds.Contains(m.HomeTeamId) ||
                    !teamIds.Contains(m.AwayTeamId) ||
                    m.HomeTeamId == m.AwayTeamId))
                {
                    throw new InvalidOperationException(
                        $"Invalid participants in group {group.GroupId}.");
                }

                var pairCounts = groupMatches
                    .GroupBy(m => string.Join("|",
                        new[] { m.HomeTeamId, m.AwayTeamId }
                            .OrderBy(id => id, StringComparer.Ordinal)))
                    .ToList();

                int expectedPairs = teamCount * (teamCount - 1) / 2;

                if (pairCounts.Count != expectedPairs ||
                    pairCounts.Any(p => p.Count() != matchesPerPair))
                {
                    throw new InvalidOperationException(
                        $"Invalid match pairings in group {group.GroupId}.");
                }
            }
            var standings = _standings.Generate(
                tournament,
                allMatches,
                groups,
                participantStates);

            var rankedTeams = new Dictionary<string, List<string>>();

            foreach (var group in groups)
            {
                var section = standings.Sections.SingleOrDefault(
                    s => s.SectionId == group.GroupId);

                if (section == null)
                    throw new InvalidOperationException(
                        $"Standings for {group.GroupId} not found.");

                var ranked = section.Entries
                    .Where(e => !e.IsDisqualified)
                    .OrderByDescending(e => e.Points)
                    .ThenByDescending(e => e.ScoreDifference)
                    .ThenByDescending(e => e.ScoreFor)
                    .ThenBy(e => e.ParticipantId, StringComparer.Ordinal)
                    .Take(group.AdvancingCount)
                    .Select(e => e.ParticipantId)
                    .ToList();

                if (ranked.Count != group.AdvancingCount)
                    throw new InvalidOperationException(
                        $"Not enough active teams in {group.GroupId}.");

                rankedTeams[group.GroupId] = ranked;
            }

            var slots = GroupStagePlayoffSeeder.CreateSlots(
                groups, rankedTeams);

            var participantIds = tournament.TournamentParticipants
                .Select(t => t.Id)
                .ToHashSet();

            if (slots.Any(id => !participantIds.Contains(id)))
                throw new InvalidOperationException(
                    "Unknown playoff participant.");

            if (slots.Count != tournament.Settings.ParticipantsAdvancing)
                throw new InvalidOperationException(
                    "Playoff participant count does not match settings.");

            var generated = _olympic.GenerateFirstRound(
                tournament, slots, PlayoffStageId);

            var state = new OlympicBracketState
            {
                TournamentId = tournamentId,
                StageId = PlayoffStageId,
                ParticipantCount = slots.Count,
                BracketSize = slots.Count,
                CurrentRound = 1,
                Slots = slots.Select(id => (string?)id).ToList()
            };

            await _brackets.CreateAsync(session, state);

            if (generated.Count > 0)
            {
                await _matches.CreateManyAsync(
                    session, generated);
            }

            await session.CommitTransactionAsync();

            var byes = new List<string>();

            return OlympicBracketMapper.ToResponse(
                state, generated, byes);
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }
}
