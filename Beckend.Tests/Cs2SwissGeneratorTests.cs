
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentGenerators;
using Xunit;

using Match = Beckend.Models.Match;

namespace Beckend.Tests;

public class Cs2SwissGeneratorTests
{
    private readonly Cs2SwissGenerator _generator = new();

    private static Tournament CreateTournament(
        int teamCount = 8,
        int wins = 3,
        int losses = 3)
    {
        return new Tournament
        {
            Id = "507f1f77bcf86cd799439011",
            TournamentType = TournamentType.Cs2SwissSystem,
            TournamentParticipants = Enumerable.Range(1, teamCount)
                .Select(i => new Team
                {
                    Id = $"507f1f77bcf86cd799{i:0000000000}",
                    TeamName = $"Team {i}"
                })
                .ToList(),
            Settings = new TournamentSettings
            {
                WinsToAdvance = wins,
                LossesToEliminate = losses,
                AllowDraws = false,
                AllowRematches = false
            }
        };
    }

    [Theory]
    [InlineData(8, 4)]
    [InlineData(16, 8)]
    public void FirstRound_ShouldGenerateCorrectMatchCount(
        int teamCount,
        int expectedMatches)
    {
        var tournament = CreateTournament(teamCount);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<SwissBye>());

        Assert.Equal(expectedMatches, result.Matches.Count);
        Assert.Null(result.Bye);

        Assert.All(result.Matches, match =>
        {
            Assert.Equal(1, match.RoundNumber);
            Assert.Equal(tournament.Id, match.TournamentId);
            Assert.True(match.IsAutomatic);
            Assert.Equal(MatchStatus.Planing, match.Status);
        });
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void FirstRound_ShouldUseEveryTeamExactlyOnce(
        int teamCount)
    {
        var tournament = CreateTournament(teamCount);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<SwissBye>());

        var teamIds = result.Matches
            .SelectMany(m => new[]
            {
                m.HomeTeamId,
                m.AwayTeamId
            })
            .ToList();

        Assert.Equal(teamCount, teamIds.Count);
        Assert.Equal(teamCount, teamIds.Distinct().Count());
    }

    [Fact]
    public void SecondRound_ShouldPairTeamsWithSameRecord()
    {
        var tournament = CreateTournament(8);
        var teams = tournament.TournamentParticipants;

        var previousMatches = new List<Match>();

        for (int i = 0; i < 8; i += 2)
        {
            previousMatches.Add(new Match
            {
                TournamentId = tournament.Id,
                HomeTeamId = teams[i].Id,
                AwayTeamId = teams[i + 1].Id,
                RoundNumber = 1,
                Status = MatchStatus.Completed,
                HomeScore = 2,
                AwayScore = 0
            });
        }

        var result = _generator.Generate(
            tournament,
            previousMatches,
            new List<SwissBye>());

        Assert.Equal(4, result.Matches.Count);
        Assert.All(result.Matches,
            m => Assert.Equal(2, m.RoundNumber));

        var winners = teams
            .Where((_, i) => i % 2 == 0)
            .Select(t => t.Id)
            .ToHashSet();

        var losers = teams
            .Where((_, i) => i % 2 == 1)
            .Select(t => t.Id)
            .ToHashSet();

        Assert.All(result.Matches, match =>
        {
            Assert.True(
                (winners.Contains(match.HomeTeamId) &&
                 winners.Contains(match.AwayTeamId)) ||
                (losers.Contains(match.HomeTeamId) &&
                 losers.Contains(match.AwayTeamId)));
        });
    }

    [Fact]
    public void QualifiedTeams_ShouldNotPlayAgain()
    {
        var tournament = CreateTournament(
            teamCount: 4,
            wins: 1,
            losses: 2);

        var teams = tournament.TournamentParticipants;

        var previousMatches = new List<Match>
        {
            new()
            {
                TournamentId = tournament.Id,
                HomeTeamId = teams[0].Id,
                AwayTeamId = teams[1].Id,
                RoundNumber = 1,
                Status = MatchStatus.Completed,
                HomeScore = 2,
                AwayScore = 0
            },
            new()
            {
                TournamentId = tournament.Id,
                HomeTeamId = teams[2].Id,
                AwayTeamId = teams[3].Id,
                RoundNumber = 1,
                Status = MatchStatus.Completed,
                HomeScore = 2,
                AwayScore = 0
            }
        };

        var result = _generator.Generate(
            tournament,
            previousMatches,
            new List<SwissBye>());

        Assert.Single(result.Matches);

        var match = result.Matches[0];

        Assert.Contains(match.HomeTeamId,
            new[] { teams[1].Id, teams[3].Id });

        Assert.Contains(match.AwayTeamId,
            new[] { teams[1].Id, teams[3].Id });
    }

    [Fact]
    public void Draw_ShouldBeRejected()
    {
        var tournament = CreateTournament(4);
        var teams = tournament.TournamentParticipants;

        var previousMatches = new List<Match>
        {
            new()
            {
                TournamentId = tournament.Id,
                HomeTeamId = teams[0].Id,
                AwayTeamId = teams[1].Id,
                RoundNumber = 1,
                Status = MatchStatus.Completed,
                HomeScore = 1,
                AwayScore = 1
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament,
                previousMatches,
                new List<SwissBye>()));
    }

    [Fact]
public void OddGroups_ShouldGenerateCrossGroupPairing()
{
    var tournament = CreateTournament(6);
    var teams = tournament.TournamentParticipants;

    var previousMatches = new List<Match>();

    for (int i = 0; i < 6; i += 2)
    {
        previousMatches.Add(new Match
        {
            TournamentId = tournament.Id,
            HomeTeamId = teams[i].Id,
            AwayTeamId = teams[i + 1].Id,
            RoundNumber = 1,
            Status = MatchStatus.Completed,
            HomeScore = 2,
            AwayScore = 0
        });
    }

    var result = _generator.Generate(
        tournament,
        previousMatches,
        new List<SwissBye>());

    Assert.Equal(3, result.Matches.Count);

    var allIds = result.Matches
        .SelectMany(m => new[]
        {
            m.HomeTeamId,
            m.AwayTeamId
        })
        .ToList();

    Assert.Equal(6, allIds.Distinct().Count());

    var previousPairs = previousMatches
        .Select(m => string.Join("|",
            new[] { m.HomeTeamId, m.AwayTeamId }
                .OrderBy(id => id)))
        .ToHashSet();

    Assert.All(result.Matches, match =>
    {
        var key = string.Join("|",
            new[] { match.HomeTeamId, match.AwayTeamId }
                .OrderBy(id => id));

        Assert.DoesNotContain(key, previousPairs);
        Assert.Equal(2, match.RoundNumber);
    });

    var winners = teams
        .Where((_, i) => i % 2 == 0)
        .Select(t => t.Id)
        .ToHashSet();

    var crossGroupMatches = result.Matches.Count(m =>
        winners.Contains(m.HomeTeamId) !=
        winners.Contains(m.AwayTeamId));

    Assert.Equal(1, crossGroupMatches);
    }

[Fact]
public void RematchConflict_ShouldFindAlternativePairs()
{
    var tournament = CreateTournament(4);
    var teams = tournament.TournamentParticipants;

    // Після двох раундів усі команди мають результат 1-1.
    // Пари A-B та A-C вже зустрічалися.
    var previousMatches = new List<Match>
    {
        Completed(teams[0], teams[1], 1),
        Completed(teams[2], teams[3], 1),
        Completed(teams[2], teams[0], 2),
        Completed(teams[1], teams[3], 2)
    };

    var result = _generator.Generate(
        tournament,
        previousMatches,
        new List<SwissBye>());

    Assert.Equal(2, result.Matches.Count);

    var oldPairs = previousMatches
        .Select(m => PairKey(m.HomeTeamId, m.AwayTeamId))
        .ToHashSet();

    Assert.All(result.Matches, match =>
        Assert.DoesNotContain(
            PairKey(match.HomeTeamId, match.AwayTeamId),
            oldPairs));

    Assert.Equal(4, result.Matches
        .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
        .Distinct()
        .Count());
}

[Fact]
public void RematchesDisabled_ShouldThrowWhenNoPairingExists()
{
    var tournament = CreateTournament(4, wins: 5, losses: 5);
    var teams = tournament.TournamentParticipants;

    // Кожна команда вже грала з кожною.
    var previousMatches = new List<Match>
    {
        Completed(teams[0], teams[1], 1),
        Completed(teams[2], teams[3], 1),

        Completed(teams[0], teams[2], 2),
        Completed(teams[1], teams[3], 2),

        Completed(teams[0], teams[3], 3),
        Completed(teams[1], teams[2], 3)
    };

    Assert.Throws<InvalidOperationException>(() =>
        _generator.Generate(
            tournament,
            previousMatches,
            new List<SwissBye>()));
}

[Fact]
public void RematchesAllowed_ShouldGeneratePairsWhenAllTeamsMet()
{
    var tournament = CreateTournament(4, wins: 5, losses: 5);
    tournament.Settings.AllowRematches = true;

    var teams = tournament.TournamentParticipants;

    var previousMatches = new List<Match>
    {
        Completed(teams[0], teams[1], 1),
        Completed(teams[2], teams[3], 1),

        Completed(teams[0], teams[2], 2),
        Completed(teams[1], teams[3], 2),

        Completed(teams[0], teams[3], 3),
        Completed(teams[1], teams[2], 3)
    };

    var result = _generator.Generate(
        tournament,
        previousMatches,
        new List<SwissBye>());

    Assert.Equal(2, result.Matches.Count);
    Assert.All(result.Matches,
        match => Assert.Equal(4, match.RoundNumber));
}

[Fact]
public void AdaptivePairing_ShouldMinimizeCrossGroupMatches()
{
    var tournament = CreateTournament(8);
    var teams = tournament.TournamentParticipants;

    // Чотири переможці (1-0) та чотири команди (0-1).
    // Усі групи парні, тому міжгрупових матчів бути не повинно.
    var previousMatches = new List<Match>
    {
        Completed(teams[0], teams[1], 1),
        Completed(teams[2], teams[3], 1),
        Completed(teams[4], teams[5], 1),
        Completed(teams[6], teams[7], 1)
    };

    var result = _generator.Generate(
        tournament,
        previousMatches,
        new List<SwissBye>());

    var winners = new HashSet<string>
    {
        teams[0].Id,
        teams[2].Id,
        teams[4].Id,
        teams[6].Id
    };

    int crossGroupMatches = result.Matches.Count(m =>
        winners.Contains(m.HomeTeamId) !=
        winners.Contains(m.AwayTeamId));

    Assert.Equal(0, crossGroupMatches);
}

private static Match Completed(
    Team home,
    Team away,
    int round)
{
    return new Match
    {
        HomeTeamId = home.Id,
        AwayTeamId = away.Id,
        TournamentId = "507f1f77bcf86cd799439011",
        RoundNumber = round,
        Status = MatchStatus.Completed,
        HomeScore = 2,
        AwayScore = 0
    };
}

private static string PairKey(string first, string second)
{
    return string.Join("|",
        new[] { first, second }.OrderBy(id => id));
}

}
