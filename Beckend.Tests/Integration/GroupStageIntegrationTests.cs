using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.TournamentGroups;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Matches.Generation;
using Beckend.Services.TournamentGenerators;
using Beckend.Services.TournamentStandings;
using Moq;
using MongoDB.Driver;
using Xunit;
using Match = Beckend.Models.Match;

namespace Beckend.Tests.Integration;

public class GroupStageIntegrationTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly TournamentGroupRepository _groups;
    private readonly MatchGenerationService _generation;
    private readonly GroupStageStandingsGenerator _standings;
    

    public GroupStageIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _tournaments = new TournamentRepository(fixture.Database);
        _matches = new MatchRepository(fixture.Database);
        _groups = new TournamentGroupRepository(fixture.Database);

        var roundRobin = new RoundRobinGenerator();
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<List<MatchDto>>(It.IsAny<object>()))
            .Returns(new List<MatchDto>());

        _generation = new MatchGenerationService(
            _matches,
            _tournaments,
            new SwissByeRepository(fixture.Database),
            roundRobin,
            new FixedRoundsSwissGenerator(),
            new ThresholdSwissGenerator(),
            fixture.Database.Client,
            mapper.Object,
            new Cs2SwissGenerator(),
            new GroupStageGenerator(roundRobin),
            _groups,
            new DoubleEliminationGenerator());

        _standings = new GroupStageStandingsGenerator(
            _groups,
            new RoundRobinStandingsGenerator());
            
    }

    private async Task<Tournament> CreateTournamentAsync(
        int teamCount = 16,
        int groupsCount = 4,
        int advancing = 8,
        int matchesPerPair = 1,
        bool shuffle = false)
    {
        var tournament = new Tournament
        {
            TournamentName = $"GroupStage integration {Guid.NewGuid():N}",
            TournamentType = TournamentType.GroupStage,
            Status = TournamentStatus.Ready,
            TournamentParticipants = Enumerable.Range(1, teamCount)
                .Select(i => new Team { TeamName = $"Team {i}" })
                .ToList(),
            Settings = new TournamentSettings
            {
                GroupsCount = groupsCount,
                ParticipantsAdvancing = advancing,
                MatchesPerPair = matchesPerPair,
                ShuffleParticipants = shuffle,
                AllowDraws = true,
                PointsForWin = 3,
                PointsForDraw = 1,
                PointsForLoss = 0
            }
        };

        await _fixture.Database
            .GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);

        return tournament;
    }
    private Task<List<Match>> SavedMatchesAsync(string id) =>
        _matches.GetMatchesByTournamentIdAsync(id);

    private Task<List<TournamentGroup>> SavedGroupsAsync(string id) =>
        _groups.GetByTournamentIdAsync(id);

    private async Task CompleteMatchAsync(Match match, int home, int away)
    {
        var update = Builders<Match>.Update
            .Set(m => m.HomeScore, home)
            .Set(m => m.AwayScore, away)
            .Set(m => m.WinnerId,
                home == away ? null : home > away ? match.HomeTeamId : match.AwayTeamId)
            .Set(m => m.Status, MatchStatus.Completed);

        var result = await _fixture.Database
            .GetCollection<Match>("Matches")
            .UpdateOneAsync(m => m.Id == match.Id, update);

        Assert.Equal(1, result.MatchedCount);
    }

    [Fact]
    public async Task Generate_ShouldPersistFourGroupsAndTwentyFourMatches()
    {
        var tournament = await CreateTournamentAsync();

        await _generation.GenerateGroupStageAsync(tournament.Id);

        var groups = await SavedGroupsAsync(tournament.Id);
        var matches = await SavedMatchesAsync(tournament.Id);
        var savedTournament = await _tournaments.GetByIdAsync(tournament.Id);

        Assert.Equal(4, groups.Count);
        Assert.Equal(24, matches.Count);
        Assert.Equal(TournamentStatus.InProgress, savedTournament!.Status);
        Assert.Equal(16, groups.SelectMany(g => g.TeamIds).Distinct().Count());
        Assert.All(groups, g =>
        {
            Assert.Equal(tournament.Id, g.TournamentId);
            Assert.Equal(4, g.TeamIds.Count);
            Assert.Equal(2, g.AdvancingCount);
            Assert.Equal(6, matches.Count(m => m.GroupId == g.GroupId));
        });
        Assert.All(matches, m =>
        {
            Assert.Equal(tournament.Id, m.TournamentId);
            Assert.Equal("groups", m.StageId);
            Assert.True(m.IsAutomatic);
            Assert.InRange(m.RoundNumber, 1, 3);
            Assert.NotEqual(m.HomeTeamId, m.AwayTeamId);
        });
    }

    [Fact]
    public async Task Generate_ShouldNotMixTeamsBetweenGroups()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateGroupStageAsync(tournament.Id);

        var groups = await SavedGroupsAsync(tournament.Id);
        var matches = await SavedMatchesAsync(tournament.Id);

        foreach (var group in groups)
        {
            var teamIds = group.TeamIds.ToHashSet();
            var groupMatches = matches.Where(m => m.GroupId == group.GroupId).ToList();
            Assert.All(groupMatches, m =>
            {
                Assert.Contains(m.HomeTeamId, teamIds);
                Assert.Contains(m.AwayTeamId, teamIds);
            });
            Assert.Equal(6, groupMatches
                .Select(m => string.Join("|", new[] { m.HomeTeamId, m.AwayTeamId }
                    .OrderBy(id => id)))
                .Distinct().Count());
        }
    }
    [Fact]
    public async Task Generate_WithTwoMatchesPerPair_ShouldCreateFortyEightMatches()
    {
        var tournament = await CreateTournamentAsync(matchesPerPair: 2);
        await _generation.GenerateGroupStageAsync(tournament.Id);

        var matches = await SavedMatchesAsync(tournament.Id);
        Assert.Equal(48, matches.Count);
        Assert.All(await SavedGroupsAsync(tournament.Id), group =>
            Assert.Equal(12, matches.Count(m => m.GroupId == group.GroupId)));
    }

    [Fact]
    public async Task Generate_Twice_ShouldRejectSecondCallAndKeepData()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateGroupStageAsync(tournament.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _generation.GenerateGroupStageAsync(tournament.Id));

        Assert.Equal(24, (await SavedMatchesAsync(tournament.Id)).Count);
        Assert.Equal(4, (await SavedGroupsAsync(tournament.Id)).Count);
    }

    [Theory]
    [InlineData(16, 0, 8)]
    [InlineData(16, 4, 7)]
    [InlineData(16, 4, 16)]
    [InlineData(15, 4, 8)]
    [InlineData(4, 4, 2)]
    public async Task Generate_InvalidSettings_ShouldNotPersistAnything(
        int teamCount, int groupCount, int advancing)
    {
        var tournament = await CreateTournamentAsync(
            teamCount: teamCount,
            groupsCount: groupCount,
            advancing: advancing);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _generation.GenerateGroupStageAsync(tournament.Id));

        Assert.Empty(await SavedMatchesAsync(tournament.Id));
        Assert.Empty(await SavedGroupsAsync(tournament.Id));
        Assert.Equal(TournamentStatus.Ready,
            (await _tournaments.GetByIdAsync(tournament.Id))!.Status);
    }
    [Fact]
    public async Task Standings_BeforeResults_ShouldContainAllTeamsWithZeroPoints()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateGroupStageAsync(tournament.Id);

        var result = await _standings.GenerateAsync(
            tournament, await SavedMatchesAsync(tournament.Id));

        Assert.Equal("GroupStage", result.TournamentType);
        Assert.Equal("Groups", result.ViewType);
        Assert.Equal(4, result.Sections.Count);
        Assert.All(result.Sections, section =>
        {
            Assert.Equal(4, section.Entries.Count);
            Assert.All(section.Entries, entry =>
            {
                Assert.Equal(0, entry.Played);
                Assert.Equal(0m, entry.Points);
            });
        });
    }

    [Fact]
    public async Task Standings_ShouldCalculateResultsIndependentlyPerGroup()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateGroupStageAsync(tournament.Id);

        var matches = await SavedMatchesAsync(tournament.Id);
        var groups = await SavedGroupsAsync(tournament.Id);
        var firstGroup = groups[0];
        var firstMatch = matches.First(m => m.GroupId == firstGroup.GroupId);
        await CompleteMatchAsync(firstMatch, 3, 1);

        var result = await _standings.GenerateAsync(
            tournament, await SavedMatchesAsync(tournament.Id));

        var section = Assert.Single(result.Sections,
            s => s.SectionId == firstGroup.GroupId);
        var winner = Assert.Single(section.Entries,
            e => e.ParticipantId == firstMatch.HomeTeamId);
        var loser = Assert.Single(section.Entries,
            e => e.ParticipantId == firstMatch.AwayTeamId);

        Assert.Equal(3m, winner.Points);
        Assert.Equal(1, winner.Wins);
        Assert.Equal(2, winner.ScoreDifference);
        Assert.Equal(0m, loser.Points);
        Assert.Equal(1, loser.Losses);
        Assert.Equal(-2, loser.ScoreDifference);

        Assert.All(result.Sections.Where(s => s.SectionId != firstGroup.GroupId),
            s => Assert.All(s.Entries, e => Assert.Equal(0, e.Played)));
    }
    [Fact]
    public async Task Standings_Draw_ShouldAwardOnePointToBothTeams()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateGroupStageAsync(tournament.Id);

        var match = (await SavedMatchesAsync(tournament.Id))[0];
        await CompleteMatchAsync(match, 2, 2);

        var result = await _standings.GenerateAsync(
            tournament, await SavedMatchesAsync(tournament.Id));
        var entries = result.Sections
            .Single(s => s.SectionId == match.GroupId).Entries;

        Assert.Equal(1m, entries.Single(e => e.ParticipantId == match.HomeTeamId).Points);
        Assert.Equal(1m, entries.Single(e => e.ParticipantId == match.AwayTeamId).Points);
        Assert.Equal(1, entries.Single(e => e.ParticipantId == match.HomeTeamId).Draws);
        Assert.Equal(1, entries.Single(e => e.ParticipantId == match.AwayTeamId).Draws);
    }

    [Fact]
    public async Task Standings_CompletedMatchWithoutScore_ShouldFail()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateGroupStageAsync(tournament.Id);

        var match = (await SavedMatchesAsync(tournament.Id))[0];
        await _fixture.Database.GetCollection<Match>("Matches")
            .UpdateOneAsync(m => m.Id == match.Id,
                Builders<Match>.Update.Set(m => m.Status, MatchStatus.Completed));

        var savedMatches = await SavedMatchesAsync(tournament.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _standings.GenerateAsync(tournament, savedMatches));
    }
}