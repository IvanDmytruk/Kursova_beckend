using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentGenerators;
using Xunit;

namespace Beckend.Tests;

public class SwissGeneratorTests
{
    private readonly FixedRoundsSwissGenerator _fixed = new();
    private readonly ThresholdSwissGenerator _threshold = new();

    private static Tournament CreateTournament(
        int teamCount,
        SwissMode mode,
        bool allowRematches = false)
    {
        var teams = Enumerable.Range(1, teamCount)
            .Select(i => new Team
            {
                Id = i.ToString("D24"),
                TeamName = $"Team {i}"
            })
            .ToList();

        return new Tournament
        {
            Id = "aaaaaaaaaaaaaaaaaaaaaaaa",
            TournamentType = TournamentType.SwissSystem,
            TournamentParticipants = teams,
            Settings = new TournamentSettings
            {
                SwissMode = mode,
                RoundsCount = 5,
                WinsToAdvance = 3,
                LossesToEliminate = 3,
                AllowDraws = false,
                AllowRematches = allowRematches,
                ByeCountsAsWin = true,
                PointsForWin = 3,
                PointsForLoss = 0,
                PointsForDraw = 1,
                PointsForBye = 3
            }
        };
    }

    private static void AssertValidRound(
        SwissRoundGenerationResult result,
        Tournament tournament)
    {
        var ids = result.Matches
            .SelectMany(m => new[]
            {
                m.HomeTeamId,
                m.AwayTeamId
            })
            .ToList();

        if (result.Bye != null)
            ids.Add(result.Bye.TeamId);

        Assert.Equal(ids.Count, ids.Distinct().Count());

        Assert.Equal(
            tournament.TournamentParticipants.Count,
            ids.Count);

        Assert.All(result.Matches, match =>
        {
            Assert.NotEqual(match.HomeTeamId, match.AwayTeamId);
            Assert.Equal(tournament.Id, match.TournamentId);
            Assert.True(match.IsAutomatic);
        });
    }

    [Theory]
    [InlineData(4, 2, false)]
    [InlineData(5, 2, true)]
    [InlineData(6, 3, false)]
    [InlineData(7, 3, true)]
    [InlineData(8, 4, false)]
    public void FixedRounds_FirstRound_GeneratesCorrectMatches(
        int teamCount,
        int expectedMatches,
        bool expectedBye)
    {
        var tournament = CreateTournament(
            teamCount,
            SwissMode.FixedRounds);

        var result = _fixed.Generate(
            tournament,
            new List<Match>(),
            new List<SwissBye>());

        Assert.Equal(expectedMatches, result.Matches.Count);
        Assert.Equal(expectedBye, result.Bye != null);

        AssertValidRound(result, tournament);
    }

    [Theory]
    [InlineData(4, 2, false)]
    [InlineData(5, 2, true)]
    [InlineData(6, 3, false)]
    [InlineData(7, 3, true)]
    [InlineData(8, 4, false)]
    public void Threshold_FirstRound_GeneratesCorrectMatches(
        int teamCount,
        int expectedMatches,
        bool expectedBye)
    {
        var tournament = CreateTournament(
            teamCount,
            SwissMode.WinLossThreshold);

        var result = _threshold.Generate(
            tournament,
            new List<Match>(),
            new List<SwissBye>());

        Assert.Equal(expectedMatches, result.Matches.Count);
        Assert.Equal(expectedBye, result.Bye != null);

        AssertValidRound(result, tournament);
    }

    [Fact]
    public void FixedRounds_DoesNotAllowGeneratingBeyondLimit()
    {
        var tournament = CreateTournament(
            4,
            SwissMode.FixedRounds);

        tournament.Settings.RoundsCount = 1;

        var previousMatches = new List<Match>
        {
            CompletedMatch(
                tournament, 0, 1, 1, 2, 0),
            CompletedMatch(
                tournament, 2, 3, 1, 1, 0)
        };

        Assert.Throws<InvalidOperationException>(() =>
            _fixed.Generate(
                tournament,
                previousMatches,
                new List<SwissBye>()));
    }

    [Fact]
    public void FixedRounds_DoesNotGenerateBeforeResults()
    {
        var tournament = CreateTournament(
            4,
            SwissMode.FixedRounds);

        var previousMatches = new List<Match>
        {
            new Match
            {
                TournamentId = tournament.Id,
                HomeTeamId =
                    tournament.TournamentParticipants[0].Id,
                AwayTeamId =
                    tournament.TournamentParticipants[1].Id,
                RoundNumber = 1,
                Status = MatchStatus.Planing
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            _fixed.Generate(
                tournament,
                previousMatches,
                new List<SwissBye>()));
    }

    [Fact]
    public void Threshold_DoesNotAllowDraws()
    {
        var tournament = CreateTournament(
            4,
            SwissMode.WinLossThreshold);

        var previousMatches = new List<Match>
        {
            CompletedMatch(
                tournament, 0, 1, 1, 1, 1)
        };

        Assert.Throws<InvalidOperationException>(() =>
            _threshold.Generate(
                tournament,
                previousMatches,
                new List<SwissBye>()));
    }

    [Fact]
    public void Threshold_RejectsInvalidThresholds()
    {
        var tournament = CreateTournament(
            4,
            SwissMode.WinLossThreshold);

        tournament.Settings.WinsToAdvance = 0;

        Assert.Throws<InvalidOperationException>(() =>
            _threshold.Generate(
                tournament,
                new List<Match>(),
                new List<SwissBye>()));
    }

    [Fact]
    public void Threshold_RejectsMoreThan16Teams()
    {
        var tournament = CreateTournament(
            17,
            SwissMode.WinLossThreshold);

        Assert.Throws<NotSupportedException>(() =>
            _threshold.Generate(
                tournament,
                new List<Match>(),
                new List<SwissBye>()));
    }

    [Fact]
    public void ByeSelector_PrefersTeamWithFewerByes()
    {
        var tournament = CreateTournament(
            5,
            SwissMode.FixedRounds);

        var teams = tournament.TournamentParticipants;

        var previousByes = new List<SwissBye>
        {
            new SwissBye
            {
                TournamentId = tournament.Id,
                TeamId = teams[0].Id,
                RoundNumber = 1
            }
        };

        var candidates = SwissByeSelector.GetCandidates(
            teams,
            previousByes,
            _ => 0m);

        Assert.Equal(teams[0].Id, candidates.Last().Id);
    }

    [Fact]
    public void ByeSelector_PrefersLowestScore()
    {
        var tournament = CreateTournament(
            5,
            SwissMode.FixedRounds);

        var teams = tournament.TournamentParticipants;

        var scores = teams.ToDictionary(
            t => t.Id,
            _ => 10m);

        scores[teams[3].Id] = 1m;

        var candidates = SwissByeSelector.GetCandidates(
            teams,
            new List<SwissBye>(),
            t => scores[t.Id]);

        Assert.Equal(teams[3].Id, candidates.First().Id);
    }

    [Fact]
    public void Threshold_ByeCountsAsWin()
    {
        var tournament = CreateTournament(
            5,
            SwissMode.WinLossThreshold);

        var teams = tournament.TournamentParticipants;

        var previousByes = new List<SwissBye>
        {
            new SwissBye
            {
                TournamentId = tournament.Id,
                TeamId = teams[0].Id,
                RoundNumber = 1,
                CountsAsWin = true,
                PointsAwarded = 3
            }
        };

        var result = _threshold.Generate(
            tournament,
            new List<Match>(),
            previousByes);

        Assert.NotNull(result.Bye);
        Assert.NotEqual(teams[0].Id, result.Bye.TeamId);
    }

    [Fact]
    public void FixedRounds_AllowsDrawWhenConfigured()
    {
        var tournament = CreateTournament(
            4,
            SwissMode.FixedRounds,
            allowRematches: true);

        tournament.Settings.AllowDraws = true;

        var previousMatches = new List<Match>
        {
            CompletedMatch(
                tournament, 0, 1, 1, 1, 1),
            CompletedMatch(
                tournament, 2, 3, 1, 0, 0)
        };

        var result = _fixed.Generate(
            tournament,
            previousMatches,
            new List<SwissBye>());

        Assert.Equal(2, result.Matches.Count);
        Assert.Null(result.Bye);
    }

    private static Match CompletedMatch(
        Tournament tournament,
        int homeIndex,
        int awayIndex,
        int round,
        int homeScore,
        int awayScore)
    {
        return new Match
        {
            TournamentId = tournament.Id,
            HomeTeamId =
                tournament.TournamentParticipants[homeIndex].Id,
            AwayTeamId =
                tournament.TournamentParticipants[awayIndex].Id,
            RoundNumber = round,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Status = MatchStatus.Completed,
            IsAutomatic = true
        };
    }
}
