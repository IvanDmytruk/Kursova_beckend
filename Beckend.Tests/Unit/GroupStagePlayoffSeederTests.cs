using Beckend.Models;
using Beckend.Services.TournamentGenerators;
using Xunit;

namespace Beckend.Tests.Unit;

public class GroupStagePlayoffSeederTests
{
    private static (
        List<TournamentGroup> Groups,
        Dictionary<string, List<string>> Ranked)
        CreateData(int groupCount)
    {
        var groups = new List<TournamentGroup>();
        var ranked = new Dictionary<string, List<string>>();

        for (int i = 1; i <= groupCount; i++)
        {
            string groupId = $"group-{i}";
            string first = $"team-{i}-1";
            string second = $"team-{i}-2";

            groups.Add(new TournamentGroup
            {
                GroupId = groupId,
                TeamIds = new List<string> { first, second },
                AdvancingCount = 2
            });

            ranked[groupId] = new List<string>
            {
                first,
                second
            };
        }

        return (groups, ranked);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void CreateSlots_ShouldGenerateUniqueParticipants(
        int groupCount)
    {
        var (groups, ranked) = CreateData(groupCount);

        var slots = GroupStagePlayoffSeeder.CreateSlots(
            groups, ranked);

        Assert.Equal(groupCount * 2, slots.Count);
        Assert.Equal(slots.Count, slots.Distinct().Count());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void CreateSlots_ShouldAvoidSameGroupInFirstRound(
        int groupCount)
    {
        var (groups, ranked) = CreateData(groupCount);

        var slots = GroupStagePlayoffSeeder.CreateSlots(
            groups, ranked);

        for (int i = 0; i < slots.Count; i += 2)
        {
            var homeGroup = groups.Single(g =>
                g.TeamIds.Contains(slots[i]));

            var awayGroup = groups.Single(g =>
                g.TeamIds.Contains(slots[i + 1]));

            Assert.NotEqual(
                homeGroup.GroupId,
                awayGroup.GroupId);
        }
    }

    [Fact]
    public void CreateSlots_ShouldRejectInsufficientTeams()
    {
        var (groups, ranked) = CreateData(4);

        ranked["group-1"].RemoveAt(1);

        Assert.Throws<InvalidOperationException>(() =>
            GroupStagePlayoffSeeder.CreateSlots(
                groups, ranked));
    }

    [Fact]
    public void CreateSlots_ShouldRejectDuplicateTeams()
    {
        var (groups, ranked) = CreateData(4);

        ranked["group-2"][0] = ranked["group-1"][0];

        Assert.Throws<InvalidOperationException>(() =>
            GroupStagePlayoffSeeder.CreateSlots(
                groups, ranked));
    }

    [Fact]
    public void CreateSlots_ShouldRejectUnsupportedGroupCount()
    {
        var (groups, ranked) = CreateData(3);

        Assert.Throws<InvalidOperationException>(() =>
            GroupStagePlayoffSeeder.CreateSlots(
                groups, ranked));
    }
}