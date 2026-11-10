using Beckend.Models;
using Beckend.Services.TournamentGenerators;
using Xunit;

namespace Beckend.Tests.Olympic;

public class OlympicGeneratorTests
{
    private readonly OlympicGenerator _generator = new();

    private static List<Team> CreateTeams(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Team($"Team {i}"))
            .ToList();
    }

    private static Tournament CreateTournament()
    {
        return new Tournament
        {
            TournamentName = "Olympic Test"
        };
    }

    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 4, 0)]
    [InlineData(5, 8, 3)]
    [InlineData(6, 8, 2)]
    [InlineData(7, 8, 1)]
    [InlineData(8, 8, 0)]
    public void CreateSlots_ShouldGenerateCorrectBracket(
        int teamCount,
        int expectedBracketSize,
        int expectedByes)
    {
        var teams = CreateTeams(teamCount);

        var slots = _generator.CreateSlots(
            teams, shuffle: false);

        Assert.Equal(expectedBracketSize, slots.Count);

        Assert.Equal(
            expectedByes,
            slots.Count(x => x == null));

        Assert.Equal(
            teamCount,
            slots.Count(x => x != null));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void CreateSlots_ShouldContainEveryTeamExactlyOnce(
        int teamCount)
    {
        var teams = CreateTeams(teamCount);

        var slots = _generator.CreateSlots(
            teams, shuffle: false);

        var actualIds = slots
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        Assert.Equal(teamCount, actualIds.Distinct().Count());

        Assert.True(
            teams.Select(t => t.Id)
                .OrderBy(x => x)
                .SequenceEqual(actualIds.OrderBy(x => x)));
    }

    [Fact]
    public void CreateSlots_FourTeams_ShouldUseCorrectSeedOrder()
    {
        var teams = CreateTeams(4);

        var slots = _generator.CreateSlots(
            teams, shuffle: false);

        var expected = new[]
        {
            teams[0].Id,
            teams[3].Id,
            teams[1].Id,
            teams[2].Id
        };

        Assert.Equal(expected, slots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CreateSlots_LessThanTwoTeams_ShouldThrow(
        int count)
    {
        var teams = CreateTeams(count);

        Assert.Throws<ArgumentException>(() =>
            _generator.CreateSlots(teams, false));
    }

    [Fact]
    public void CreateSlots_DuplicateIds_ShouldThrow()
    {
        var teams = CreateTeams(2);

        teams[1].Id = teams[0].Id;

        Assert.Throws<ArgumentException>(() =>
            _generator.CreateSlots(teams, false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSlots_InvalidId_ShouldThrow(string id)
    {
        var teams = CreateTeams(2);

        teams[0].Id = id;

        Assert.Throws<ArgumentException>(() =>
            _generator.CreateSlots(teams, false));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(8, 4)]
    public void GenerateFirstRound_ShouldCreateCorrectMatchCount(
        int teamCount,
        int expectedMatches)
    {
        var teams = CreateTeams(teamCount);
        var tournament = CreateTournament();

        var slots = _generator.CreateSlots(
            teams, shuffle: false);

        var matches = _generator.GenerateFirstRound(
            tournament, slots, "stage-1");

        Assert.Equal(expectedMatches, matches.Count);
    }
    [Fact]
    public void GenerateFirstRound_ShouldSetCorrectProperties()
    {
        var teams = CreateTeams(4);
        var tournament = CreateTournament();

        var slots = _generator.CreateSlots(teams, false);

        var matches = _generator.GenerateFirstRound(
            tournament, slots, "stage-1");

        Assert.Equal(2, matches.Count);

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];

            Assert.Equal(tournament.Id, match.TournamentId);
            Assert.Equal("stage-1", match.StageId);
            Assert.Equal(1, match.RoundNumber);
            Assert.Equal(i + 1, match.BracketPosition);
            Assert.True(match.IsAutomatic);

            Assert.False(string.IsNullOrWhiteSpace(
                match.HomeTeamId));

            Assert.False(string.IsNullOrWhiteSpace(
                match.AwayTeamId));

            Assert.NotEqual(
                match.HomeTeamId,
                match.AwayTeamId);
        }
    }

    [Fact]
    public void GenerateFirstRound_ShouldSkipByeMatches()
    {
        var teams = CreateTeams(3);
        var tournament = CreateTournament();

        var slots = _generator.CreateSlots(teams, false);

        var matches = _generator.GenerateFirstRound(
            tournament, slots, "stage-1");

        Assert.Single(matches);

        Assert.All(matches, match =>
        {
            Assert.NotNull(match.HomeTeamId);
            Assert.NotNull(match.AwayTeamId);
        });
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void GenerateFirstRound_InvalidBracketSize_ShouldThrow(
        int size)
    {
        var tournament = CreateTournament();

        var slots = Enumerable
            .Range(0, size)
            .Select(i => (string?)$"team-{i}")
            .ToList();

        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateFirstRound(
                tournament, slots, "stage-1"));
    }

    [Fact]
    public void CreateSlots_WithShuffle_ShouldPreserveParticipants()
    {
        var teams = CreateTeams(8);

        var slots = _generator.CreateSlots(
            teams, shuffle: true);

        var actualIds = slots
            .Where(x => x != null)
            .Select(x => x!)
            .OrderBy(x => x);

        var expectedIds = teams
            .Select(x => x.Id)
            .OrderBy(x => x);

        Assert.Equal(expectedIds, actualIds);
    }
}
