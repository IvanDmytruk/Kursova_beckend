using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Services.Olympic;
using Xunit;

namespace Beckend.Tests.Olympic;

public class OlympicAdvancementResolverTests
{
    private static OlympicBracketState CreateState(
        params string?[] slots)
    {
        return new OlympicBracketState
        {
            TournamentId = "tournament-test",
            StageId = "main",
            ParticipantCount = slots.Count(x => x != null),
            BracketSize = slots.Length,
            CurrentRound = 1,
            Slots = slots.ToList()
        };
    }

    private static Match CreateCompletedMatch(
        int position,
        string home,
        string away,
        string winner)
    {
        return new Match
        {
            BracketPosition = position,
            RoundNumber = 1,
            HomeTeamId = home,
            AwayTeamId = away,
            WinnerId = winner,
            Status = MatchStatus.Completed
        };
    }

    [Fact]
    public void FourTeams_ShouldReturnCorrectWinners()
    {
        var state = CreateState("A", "D", "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(1, "A", "D", "A"),
            CreateCompletedMatch(2, "B", "C", "C")
        };

        var result = OlympicAdvancementResolver.Resolve(
            state, matches, 1);

        Assert.Equal(new[] { "A", "C" }, result);
    }

    [Fact]
    public void ThreeTeams_ShouldAdvanceByeAutomatically()
    {
        var state = CreateState("A", null, "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(2, "B", "C", "B")
        };

        var result = OlympicAdvancementResolver.Resolve(
            state, matches, 1);

        Assert.Equal(new[] { "A", "B" }, result);
    }

    [Fact]
    public void UnfinishedMatch_ShouldThrow()
    {
        var state = CreateState("A", "D", "B", "C");

        var match = CreateCompletedMatch(1, "A", "D", "A");
        match.Status = MatchStatus.Planing;

        var matches = new List<Match>
        {
            match,
            CreateCompletedMatch(2, "B", "C", "B")
        };

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, matches, 1));
    }

    [Fact]
    public void InvalidWinner_ShouldThrow()
    {
        var state = CreateState("A", "D", "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(1, "A", "D", "X"),
            CreateCompletedMatch(2, "B", "C", "B")
        };

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, matches, 1));
    }

    [Fact]
    public void DuplicatePosition_ShouldThrow()
    {
        var state = CreateState("A", "D", "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(1, "A", "D", "A"),
            CreateCompletedMatch(1, "B", "C", "B")
        };

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, matches, 1));
    }

    [Fact]
    public void MissingMatch_ShouldThrow()
    {
        var state = CreateState("A", "D", "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(1, "A", "D", "A")
        };

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, matches, 1));
    }

    [Fact]
    public void WrongParticipants_ShouldThrow()
    {
        var state = CreateState("A", "D", "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(1, "A", "X", "A"),
            CreateCompletedMatch(2, "B", "C", "B")
        };

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, matches, 1));
    }

    [Fact]
    public void FinalRound_ShouldReturnChampion()
    {
        var state = CreateState("A", "D", "B", "C");

        var matches = new List<Match>
        {
            CreateCompletedMatch(1, "A", "C", "C")
        };

        var result = OlympicAdvancementResolver.Resolve(
            state, matches, 2);

        Assert.Single(result);
        Assert.Equal("C", result[0]);
    }

    [Fact]
    public void InvalidBracketSize_ShouldThrow()
    {
        var state = CreateState("A", "B", "C");

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, new List<Match>(), 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void InvalidRound_ShouldThrow(int round)
    {
        var state = CreateState("A", "D", "B", "C");

        Assert.Throws<InvalidOperationException>(() =>
            OlympicAdvancementResolver.Resolve(
                state, new List<Match>(), round));
    }
}
