using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Services.Olympic;
using Beckend.Services.TournamentGenerators;
using Xunit;

namespace Beckend.Tests.Olympic;

public class OlympicGeneratorTests
{
    private readonly OlympicGenerator _generator = new();

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(6, 8)]
    [InlineData(7, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    [InlineData(12, 16)]
    [InlineData(16, 16)]
    public void CreateSlots_ShouldGenerateCorrectBracketSize(
        int participantCount,
        int expectedSize)
    {
        var teams = CreateTeams(participantCount);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        Assert.Equal(expectedSize, slots.Count);

        Assert.Equal(
            participantCount,
            slots.Count(id => id != null));

        Assert.Equal(
            participantCount,
            slots.Where(id => id != null).Distinct().Count());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(12)]
    public void FirstRound_ShouldCorrectlyHandleByes(
        int participantCount)
    {
        var teams = CreateTeams(participantCount);

        var tournament = CreateTournament(teams);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        var matches = _generator.GenerateFirstRound(
            tournament,
            slots,
            "main");

        int expectedByes = slots.Count - participantCount;
        int expectedMatches = participantCount - slots.Count / 2;

        Assert.Equal(expectedMatches, matches.Count);

        int actualByes = 0;

        for (int i = 0; i < slots.Count; i += 2)
        {
            if ((slots[i] == null) != (slots[i + 1] == null))
                actualByes++;
        }

        Assert.Equal(expectedByes, actualByes);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(12)]
    public void Resolver_ShouldAdvanceAllTeamsFromFirstRound(
        int participantCount)
    {
        var teams = CreateTeams(participantCount);
        var tournament = CreateTournament(teams);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        var matches = _generator.GenerateFirstRound(
            tournament,
            slots,
            "main");

        CompleteMatches(matches);

        var state = CreateState(
            tournament,
            slots);

        var advancing = OlympicAdvancementResolver.Resolve(
            state,
            matches,
            1);

        Assert.Equal(slots.Count / 2, advancing.Count);

        Assert.All(
            advancing,
            teamId => Assert.False(string.IsNullOrWhiteSpace(teamId)));

        Assert.Equal(
            advancing.Count,
            advancing.Distinct().Count());
    }

    [Fact]
    public void Resolver_ShouldRejectIncompleteMatch()
    {
        var teams = CreateTeams(4);
        var tournament = CreateTournament(teams);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        var matches = _generator.GenerateFirstRound(
            tournament,
            slots,
            "main");

        var state = CreateState(tournament, slots);

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state,
                matches,
                1));
    }

    [Fact]
    public void Resolver_ShouldRejectInvalidWinner()
    {
        var teams = CreateTeams(4);
        var tournament = CreateTournament(teams);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        var matches = _generator.GenerateFirstRound(
            tournament,
            slots,
            "main");

        CompleteMatches(matches);

        matches[0].WinnerId = "unknown-team";

        var state = CreateState(tournament, slots);

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state,
                matches,
                1));
    }

    [Fact]
    public void Resolver_ShouldRejectDuplicateBracketPosition()
    {
        var teams = CreateTeams(4);
        var tournament = CreateTournament(teams);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        var matches = _generator.GenerateFirstRound(
            tournament,
            slots,
            "main");

        CompleteMatches(matches);

        matches[1].BracketPosition =
            matches[0].BracketPosition;

        var state = CreateState(tournament, slots);

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state,
                matches,
                1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(16)]
    public void FullTournament_ShouldDetermineOneChampion(
        int participantCount)
    {
        var teams = CreateTeams(participantCount);
        var tournament = CreateTournament(teams);

        var slots = _generator.CreateSlots(
            teams,
            shuffle: false);

        var state = CreateState(tournament, slots);

        var currentMatches = _generator.GenerateFirstRound(
            tournament,
            slots,
            "main");

        int totalRounds = (int)Math.Log2(slots.Count);

        for (int round = 1; round <= totalRounds; round++)
        {
            CompleteMatches(currentMatches);

            var advancing = OlympicAdvancementResolver.Resolve(
                state,
                currentMatches,
                round);

            if (round == totalRounds)
            {
                Assert.Single(advancing);
                Assert.Contains(advancing[0], teams.Select(t => t.Id));
                return;
            }

            currentMatches = CreateNextRoundMatches(
                tournament,
                advancing,
                round + 1);
        }
    }

    private static List<Team> CreateTeams(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Team
            {
                Id = i.ToString("D24"),
                TeamName = $"Team {i}"
            })
            .ToList();
    }

    private static Tournament CreateTournament(
        List<Team> teams)
    {
        return new Tournament
        {
            Id = "507f1f77bcf86cd799439011",
            TournamentName = "Olympic Test",
            TournamentType = TournamentType.OlympicSystem,
            SportName = SportName.Football,
            TournamentParticipants = teams
        };
    }

    private static OlympicBracketState CreateState(
        Tournament tournament,
        List<string?> slots)
    {
        return new OlympicBracketState
        {
            TournamentId = tournament.Id,
            StageId = "main",
            ParticipantCount = tournament.TournamentParticipants.Count,
            BracketSize = slots.Count,
            CurrentRound = 1,
            Slots = slots
        };
    }

    private static void CompleteMatches(
        List<Match> matches)
    {
        foreach (var match in matches)
        {
            match.Status = MatchStatus.Completed;
            match.HomeScore = 2;
            match.AwayScore = 1;
            match.WinnerId = match.HomeTeamId;
        }
    }

    private static List<Match> CreateNextRoundMatches(
        Tournament tournament,
        List<string?> advancing,
        int round)
    {
        var matches = new List<Match>();

        for (int i = 0; i < advancing.Count; i += 2)
        {
            var home = advancing[i];
            var away = advancing[i + 1];

            if (home == null || away == null)
                continue;

            matches.Add(new Match
            {
                TournamentId = tournament.Id,
                StageId = "main",
                MatchName = $"Round {round}",
                HomeTeamId = home,
                AwayTeamId = away,
                RoundNumber = round,
                BracketPosition = i / 2 + 1,
                SportName = tournament.SportName,
                IsAutomatic = true,
                Status = MatchStatus.Planing
            });
        }

        return matches;
    }

[Fact]
public void Generate_DoubleRoundRobin_ShouldSwapHomeAndAway()
{
    var tournament = new Tournament
    {
        TournamentType = TournamentType.RoundRobin,
        Settings = new TournamentSettings
        {
            MatchesPerPair = 2,
            ShuffleParticipants = false
        },
        TournamentParticipants = new List<Team>
        {
            new Team { Id = "A", TeamName = "Team A" },
            new Team { Id = "B", TeamName = "Team B" },
            new Team { Id = "C", TeamName = "Team C" },
            new Team { Id = "D", TeamName = "Team D" }
        }
    };

    var matches = new RoundRobinGenerator().Generate(tournament);

    Assert.Equal(12, matches.Count);
    Assert.Equal(6, matches.Select(m => m.RoundNumber).Distinct().Count());

    var pairs = matches.GroupBy(m =>
        string.CompareOrdinal(m.HomeTeamId, m.AwayTeamId) < 0
            ? (m.HomeTeamId, m.AwayTeamId)
            : (m.AwayTeamId, m.HomeTeamId));

    Assert.Equal(6, pairs.Count());

    foreach (var pair in pairs)
    {
        var games = pair.ToList();

        Assert.Equal(2, games.Count);
        Assert.Equal(games[0].HomeTeamId, games[1].AwayTeamId);
        Assert.Equal(games[0].AwayTeamId, games[1].HomeTeamId);
    }
}

}