
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentGenerators;
using Xunit;

using Match = Beckend.Models.Match;

namespace Beckend.Tests;

public class DoubleEliminationGeneratorTests
{
    private readonly DoubleEliminationGenerator _generator = new();

    private static Tournament CreateTournament(int count)
    {
        return new Tournament
        {
            Id = "tournament-1",
            TournamentName = "Test Tournament",
            TournamentType = TournamentType.DoubleElimination,
            TournamentParticipants = Enumerable.Range(1, count)
                .Select(i => new Team { Id = $"team-{i}" })
                .ToList(),
            Settings = new TournamentSettings
            {
                ShuffleParticipants = false
            }
        };
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 4)]
    [InlineData(16, 8)]
    public void FirstRound_GeneratesExpectedMatches(
        int teamsCount,
        int expectedMatches)
    {
        var tournament = CreateTournament(teamsCount);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        Assert.Equal(expectedMatches, result.Matches.Count);
        Assert.Empty(result.ByeTeamIds);
        Assert.False(result.IsCompleted);

        Assert.All(result.Matches, match =>
        {
            Assert.Equal(1, match.RoundNumber);
            Assert.Equal("double-main", match.StageId);
            Assert.True(match.IsAutomatic);
            Assert.NotEqual(match.HomeTeamId, match.AwayTeamId);
        });
    }

    [Fact]
    public void OddNumberOfTeams_AssignsBye()
    {
        var tournament = CreateTournament(5);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        Assert.Equal(2, result.Matches.Count);
        Assert.Single(result.ByeTeamIds);

        string byeTeam = result.ByeTeamIds.Single();

        Assert.DoesNotContain(result.Matches,
            m => m.HomeTeamId == byeTeam ||
                 m.AwayTeamId == byeTeam);
    }

    [Fact]
    public void UnfinishedMatches_BlockNextRound()
    {
        var tournament = CreateTournament(4);

        var firstRound = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament,
                firstRound.Matches,
                new List<string>()));
    }

    [Fact]
    public void CompletedMatches_GenerateNextRound()
    {
        var tournament = CreateTournament(4);

        var firstRound = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        foreach (var match in firstRound.Matches)
        {
            match.Status = MatchStatus.Completed;
            match.HomeScore = 2;
            match.AwayScore = 1;
            match.WinnerId = match.HomeTeamId;
        }

        var secondRound = _generator.Generate(
            tournament,
            firstRound.Matches,
            firstRound.ByeTeamIds);

        Assert.Equal(2, secondRound.Matches.Count);

        Assert.Contains(secondRound.Matches,
            m => m.HomeTeamId == "team-1" &&
                 m.AwayTeamId == "team-3");

        Assert.Contains(secondRound.Matches,
            m => m.HomeTeamId == "team-2" &&
                 m.AwayTeamId == "team-4");

        Assert.All(secondRound.Matches,
            m => Assert.Equal(2, m.RoundNumber));
    }

    [Fact]
    public void DifferentRecords_AreNotPaired()
    {
        var tournament = CreateTournament(4);

        var firstRound = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        foreach (var match in firstRound.Matches)
        {
            match.Status = MatchStatus.Completed;
            match.HomeScore = 1;
            match.AwayScore = 0;
        }

        var secondRound = _generator.Generate(
            tournament,
            firstRound.Matches,
            new List<string>());

        Assert.All(secondRound.Matches, match =>
        {
            bool bothWinners =
                match.HomeTeamId is "team-1" or "team-3" &&
                match.AwayTeamId is "team-1" or "team-3";

            bool bothLosers =
                match.HomeTeamId is "team-2" or "team-4" &&
                match.AwayTeamId is "team-2" or "team-4";

            Assert.True(bothWinners || bothLosers);
        });
    }

    [Fact]
    public void DuplicateParticipantIds_AreRejected()
    {
        var tournament = CreateTournament(4);
        tournament.TournamentParticipants[1].Id =
            tournament.TournamentParticipants[0].Id;

        Assert.Throws<ArgumentException>(() =>
            _generator.Generate(
                tournament,
                new List<Match>(),
                new List<string>()));
    }

    [Fact]
    public void DrawResult_IsRejected()
    {
        var tournament = CreateTournament(4);

        var firstRound = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        foreach (var match in firstRound.Matches)
        {
            match.Status = MatchStatus.Completed;
            match.HomeScore = 1;
            match.AwayScore = 1;
        }

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament,
                firstRound.Matches,
                new List<string>()));
    }

    // Допоміжний метод: завершує всі матчі раунду.
    // За замовчуванням перемагає домашня команда.
    private static void CompleteRound(
        IEnumerable<Match> matches,
        bool homeWins = true)
    {
        foreach (var match in matches)
        {
            match.Status = MatchStatus.Completed;
            match.HomeScore = homeWins ? 2 : 1;
            match.AwayScore = homeWins ? 1 : 2;
            match.WinnerId = homeWins
                ? match.HomeTeamId
                : match.AwayTeamId;
        }
    }

    // Допоміжний метод для створення історії матчів.
    private static Match CompletedMatch(
        string home,
        string away,
        int round,
        bool homeWins = true,
        string stage = "double-main")
    {
        return new Match
        {
            TournamentId = "tournament-1",
            HomeTeamId = home,
            AwayTeamId = away,
            RoundNumber = round,
            BracketPosition = 1,
            StageId = stage,
            IsAutomatic = true,
            Status = MatchStatus.Completed,
            HomeScore = homeWins ? 2 : 1,
            AwayScore = homeWins ? 1 : 2,
            WinnerId = homeWins ? home : away
        };
    }

    [Fact]
    public void TwoTeams_RequireTwoLossesToDetermineChampion()
    {
        var tournament = CreateTournament(2);
        var history = new List<Match>();

        var round1 = _generator.Generate(
            tournament, history, new List<string>());

        Assert.Single(round1.Matches);
        CompleteRound(round1.Matches);

        history.AddRange(round1.Matches);

        var round2 = _generator.Generate(
            tournament, history, new List<string>());

        Assert.Single(round2.Matches);
        Assert.Equal("grand-final", round2.Matches[0].StageId);

        // Тепер перемагає команда, яка програла перший матч.
        CompleteRound(round2.Matches, homeWins: false);
        history.AddRange(round2.Matches);

        var round3 = _generator.Generate(
            tournament, history, new List<string>());

        Assert.Single(round3.Matches);
        Assert.Equal("grand-final", round3.Matches[0].StageId);

        CompleteRound(round3.Matches);
        history.AddRange(round3.Matches);

        var final = _generator.Generate(
            tournament, history, new List<string>());

        Assert.True(final.IsCompleted);
        Assert.Equal("team-1", final.ChampionId);
        Assert.Empty(final.Matches);
    }

    [Fact]
    public void SecondLoss_EliminatesTeam()
    {
        var tournament = CreateTournament(4);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch("team-3", "team-4", 1),
            CompletedMatch("team-2", "team-4", 2)
        };

        var result = _generator.Generate(
            tournament, history, new List<string>());

        // team-4 програла team-3 і team-2.
        Assert.DoesNotContain(result.Matches,
            m => m.HomeTeamId == "team-4" ||
                 m.AwayTeamId == "team-4");

        Assert.DoesNotContain("team-4", result.ByeTeamIds);
    }


[Fact]
public void DifferentRecords_WithoutEqualPairs_UsesFallback()
{
    var tournament = CreateTournament(3);

    var history = new List<Match>
    {
        CompletedMatch("team-1", "team-2", 1)
    };

    var result = _generator.Generate(
        tournament,
        history,
        new[] { "team-3" });

    Assert.Single(result.Matches);
    Assert.Single(result.ByeTeamIds);

    var match = result.Matches.Single();
    Assert.Contains("team-1",
        new[] { match.HomeTeamId, match.AwayTeamId });

    Assert.Contains("team-3",
        new[] { match.HomeTeamId, match.AwayTeamId });

    Assert.Equal("team-2", result.ByeTeamIds.Single());
}


    [Fact]
    public void PreviousBye_PrefersAnotherTeam()
    {
        var tournament = CreateTournament(3);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new[] { "team-1" });

        Assert.Single(result.ByeTeamIds);
        Assert.NotEqual("team-1", result.ByeTeamIds[0]);
    }

    [Fact]
    public void GeneratedRound_HasUniqueParticipants()
    {
        var tournament = CreateTournament(9);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        var allTeams = result.Matches
            .SelectMany(m => new[]
            {
                m.HomeTeamId,
                m.AwayTeamId
            })
            .Concat(result.ByeTeamIds)
            .ToList();

        Assert.Equal(9, allTeams.Count);
        Assert.Equal(9, allTeams.Distinct().Count());
    }

    [Fact]
    public void Rematch_IsAvoidedWhenAlternativeExists()
    {
        var tournament = CreateTournament(4);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch("team-3", "team-4", 1),
            CompletedMatch("team-1", "team-3", 2),
            CompletedMatch("team-2", "team-4", 2)
        };

        // Після двох раундів:
        // team-1 = 2-0
        // team-2 = 1-1
        // team-3 = 1-1
        // team-4 = 0-2
        //
        // team-2 і team-3 ще не зустрічалися.

        var result = _generator.Generate(
            tournament,
            history,
            new List<string>());

        Assert.Contains(result.Matches,
            m => (m.HomeTeamId == "team-2" &&
                  m.AwayTeamId == "team-3") ||
                 (m.HomeTeamId == "team-3" &&
                  m.AwayTeamId == "team-2"));
    }

    [Fact]
    public void UnfinishedMatch_PreventsFurtherGeneration()
    {
        var tournament = CreateTournament(4);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            new Match
            {
                TournamentId = tournament.Id,
                HomeTeamId = "team-3",
                AwayTeamId = "team-4",
                RoundNumber = 1,
                StageId = "double-main",
                IsAutomatic = true
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament, history, new List<string>()));
    }

    [Fact]
    public void InvalidHistory_EliminatedTeamCannotPlayAgain()
    {
        var tournament = CreateTournament(3);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch("team-3", "team-2", 2),
            CompletedMatch("team-1", "team-2", 3)
        };

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament, history, new List<string>()));
    }

    [Fact]
    public void GrandFinal_UndefeatedTeamWinsChampionship()
    {
        var tournament = CreateTournament(2);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch(
                "team-1", "team-2", 2,
                stage: "grand-final")
        };

        var result = _generator.Generate(
            tournament, history, new List<string>());

        Assert.True(result.IsCompleted);
        Assert.Equal("team-1", result.ChampionId);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void GrandFinalReset_LowerBracketWinnerNeedsSecondVictory()
    {
        var tournament = CreateTournament(2);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch(
                "team-2", "team-1", 2,
                stage: "grand-final")
        };

        var result = _generator.Generate(
            tournament, history, new List<string>());

        Assert.False(result.IsCompleted);
        Assert.Single(result.Matches);
        Assert.Equal("grand-final", result.Matches[0].StageId);
    }

[Fact]
public void Fallback_DoesNotPairEliminatedTeams()
{
    var tournament = CreateTournament(4);

    var history = new List<Match>
    {
        CompletedMatch("team-1", "team-2", 1),
        CompletedMatch("team-3", "team-4", 1),
        CompletedMatch("team-2", "team-4", 2)
    };

    var result = _generator.Generate(
        tournament,
        history,
        new List<string>());

    Assert.DoesNotContain(result.Matches,
        m => m.HomeTeamId == "team-4" ||
             m.AwayTeamId == "team-4");

    Assert.DoesNotContain("team-4", result.ByeTeamIds);

    // Три активні команди:
    // одна пара та один BYE.
    Assert.Single(result.Matches);
    Assert.Single(result.ByeTeamIds);
}

[Fact]
public void Fallback_PrefersUnplayedOpponent()
{
    var tournament = CreateTournament(4);

    var history = new List<Match>
    {
        CompletedMatch("team-1", "team-2", 1),
        CompletedMatch("team-3", "team-4", 1),

        CompletedMatch("team-1", "team-3", 2),
        CompletedMatch("team-2", "team-4", 2)
    };

    var result = _generator.Generate(
        tournament,
        history,
        new List<string>());

    Assert.Single(result.Matches);

    var match = result.Matches.Single();

    Assert.Contains("team-2",
        new[] { match.HomeTeamId, match.AwayTeamId });

    Assert.Contains("team-3",
        new[] { match.HomeTeamId, match.AwayTeamId });
}

[Theory]
[InlineData(3)]
[InlineData(5)]
[InlineData(8)]
[InlineData(16)]
public void FullTournament_AllHomeWins_FinishesWithOneChampion(
    int teamCount)
{
    SimulateTournament(teamCount, homeWins: true);
}

[Theory]
[InlineData(3)]
[InlineData(5)]
[InlineData(8)]
[InlineData(16)]
public void FullTournament_AllAwayWins_FinishesWithOneChampion(
    int teamCount)
{
    SimulateTournament(teamCount, homeWins: false);
}

private void SimulateTournament(int teamCount, bool homeWins)
{
    var tournament = CreateTournament(teamCount);
    var history = new List<Match>();
    var previousByes = new List<string>();

    const int maxRounds = 100;

    for (int round = 0; round < maxRounds; round++)
    {
        var result = _generator.Generate(
            tournament,
            history,
            previousByes);

        if (result.IsCompleted)
        {
            Assert.NotNull(result.ChampionId);
            Assert.Empty(result.Matches);

            Assert.Contains(
                tournament.TournamentParticipants,
                t => t.Id == result.ChampionId);

            return;
        }

        Assert.NotEmpty(result.Matches);

        previousByes.AddRange(result.ByeTeamIds);

        foreach (var match in result.Matches)
        {
            match.Status = MatchStatus.Completed;
            match.HomeScore = homeWins ? 2 : 1;
            match.AwayScore = homeWins ? 1 : 2;
            match.WinnerId = homeWins
                ? match.HomeTeamId
                : match.AwayTeamId;
        }

        history.AddRange(result.Matches);
    }

    Assert.Fail(
        $"Tournament with {teamCount} teams did not finish " +
        $"within {maxRounds} rounds.");
}

    // ===== FINAL DOUBLE ELIMINATION TESTS =====

    [Theory]
    [InlineData(3, 11)]
    [InlineData(5, 22)]
    [InlineData(8, 33)]
    [InlineData(16, 44)]
    [InlineData(32, 55)]
    public void MixedResults_TournamentAlwaysFinishes(
        int teamCount,
        int seed)
    {
        var tournament = CreateTournament(teamCount);
        var history = new List<Match>();
        var byes = new List<string>();
        var random = new Random(seed);

        for (int round = 0; round < 200; round++)
        {
            var result = _generator.Generate(
                tournament, history, byes);

            if (result.IsCompleted)
            {
                Assert.NotNull(result.ChampionId);
                Assert.Empty(result.Matches);
                Assert.Contains(
                    tournament.TournamentParticipants,
                    t => t.Id == result.ChampionId);
                return;
            }

            Assert.NotEmpty(result.Matches);

            byes.AddRange(result.ByeTeamIds);

            foreach (var match in result.Matches)
            {
                bool homeWins = random.Next(2) == 0;
                match.Status = MatchStatus.Completed;
                match.HomeScore = homeWins ? 2 : 1;
                match.AwayScore = homeWins ? 1 : 2;
                match.WinnerId = homeWins
                    ? match.HomeTeamId
                    : match.AwayTeamId;
            }

            history.AddRange(result.Matches);
        }

        Assert.Fail(
            $"Mixed tournament with {teamCount} teams " +
            "did not finish within 200 rounds.");
    }

    [Fact]
    public void Bye_DoesNotCountAsWinOrLoss()
    {
        var tournament = CreateTournament(3);

        var first = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        Assert.Single(first.ByeTeamIds);

        var byeTeam = first.ByeTeamIds.Single();

        CompleteRound(first.Matches);

        var second = _generator.Generate(
            tournament,
            first.Matches,
            first.ByeTeamIds);

        // Команда з BYE повинна залишитися активною.
        bool stillActive =
            second.Matches.Any(m =>
                m.HomeTeamId == byeTeam ||
                m.AwayTeamId == byeTeam) ||
            second.ByeTeamIds.Contains(byeTeam) ||
            second.ChampionId == byeTeam;

        Assert.True(stillActive);
    }

    [Fact]
    public void Bye_IsNotAssignedToEliminatedTeam()
    {
        var tournament = CreateTournament(4);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch("team-3", "team-4", 1),
            CompletedMatch("team-2", "team-4", 2)
        };

        var result = _generator.Generate(
            tournament,
            history,
            new[] { "team-1", "team-2", "team-3" });

        Assert.DoesNotContain(
            "team-4", result.ByeTeamIds);
    }

    [Fact]
    public void Bye_PreservesEqualRecordPair()
    {
        var tournament = CreateTournament(4);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch("team-3", "team-4", 1),
            CompletedMatch("team-1", "team-3", 2),
            CompletedMatch("team-2", "team-4", 2)
        };

        var result = _generator.Generate(
            tournament,
            history,
            new List<string>());

        Assert.Single(result.ByeTeamIds);
        Assert.Equal("team-1", result.ByeTeamIds.Single());

        Assert.Single(result.Matches);

        var match = result.Matches.Single();

        Assert.Equal(
            new[] { "team-2", "team-3" },
            new[] { match.HomeTeamId, match.AwayTeamId }
                .OrderBy(id => id)
                .ToArray());
    }

    [Fact]
    public void SameRecord_HasPriorityOverUnplayedCrossRecord()
    {
        var tournament = CreateTournament(4);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch("team-3", "team-4", 1)
        };

        var result = _generator.Generate(
            tournament,
            history,
            new List<string>());

        Assert.Equal(2, result.Matches.Count);

        Assert.Contains(result.Matches,
            m => new[] { m.HomeTeamId, m.AwayTeamId }
                .OrderBy(id => id)
                .SequenceEqual(new[] { "team-1", "team-3" }));

        Assert.Contains(result.Matches,
            m => new[] { m.HomeTeamId, m.AwayTeamId }
                .OrderBy(id => id)
                .SequenceEqual(new[] { "team-2", "team-4" }));
    }

    [Fact]
    public void GeneratedMatches_HaveUniquePositionsAndRound()
    {
        var tournament = CreateTournament(8);

        var result = _generator.Generate(
            tournament,
            new List<Match>(),
            new List<string>());

        Assert.Equal(4, result.Matches.Count);

        Assert.All(result.Matches,
            m => Assert.Equal(1, m.RoundNumber));

        Assert.Equal(
            4,
            result.Matches
                .Select(m => m.BracketPosition)
                .Distinct()
                .Count());

        Assert.All(result.Matches, m =>
        {
            Assert.True(m.IsAutomatic);
            Assert.Equal("double-main", m.StageId);
            Assert.Equal(tournament.Id, m.TournamentId);
        });
    }

    [Fact]
    public void GrandFinal_ResetWinnerBecomesChampion()
    {
        var tournament = CreateTournament(2);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1),
            CompletedMatch(
                "team-2", "team-1", 2,
                stage: "grand-final"),
            CompletedMatch(
                "team-2", "team-1", 3,
                stage: "grand-final")
        };

        var result = _generator.Generate(
            tournament,
            history,
            new List<string>());

        Assert.True(result.IsCompleted);
        Assert.Equal("team-2", result.ChampionId);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void NegativeScore_IsRejected()
    {
        var tournament = CreateTournament(2);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-2", 1)
        };

        history[0].HomeScore = -1;

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament,
                history,
                new List<string>()));
    }

    [Fact]
    public void MatchWithUnknownTeam_IsRejected()
    {
        var tournament = CreateTournament(2);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "unknown-team", 1)
        };

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament,
                history,
                new List<string>()));
    }

    [Fact]
    public void SameTeamOnBothSides_IsRejected()
    {
        var tournament = CreateTournament(2);

        var history = new List<Match>
        {
            CompletedMatch("team-1", "team-1", 1)
        };

        Assert.Throws<InvalidOperationException>(() =>
            _generator.Generate(
                tournament,
                history,
                new List<string>()));
    }

    [Fact]
    public void MatchesFromOtherTournament_AreIgnored()
    {
        var tournament = CreateTournament(2);

        var unrelated = CompletedMatch(
            "team-1", "team-2", 1);

        unrelated.TournamentId = "other-tournament";

        var result = _generator.Generate(
            tournament,
            new[] { unrelated },
            new List<string>());

        Assert.Single(result.Matches);
        Assert.Equal(1, result.Matches[0].RoundNumber);
    }

}
