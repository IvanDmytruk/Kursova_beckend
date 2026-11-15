using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public static class GroupStagePlayoffSeeder
{
    public static List<string> CreateSlots(
        IReadOnlyList<TournamentGroup> groups,
        IReadOnlyDictionary<string, List<string>> rankedTeams)
    {
        if (groups.Count < 2 ||
            (groups.Count & (groups.Count - 1)) != 0)
        {
            throw new InvalidOperationException(
                "Group count must be a power of two and at least 2.");
        }

        if (groups.Any(g => g.AdvancingCount != 2))
            throw new InvalidOperationException(
                "Exactly two teams must advance from each group.");

        var orderedGroups = groups
            .OrderBy(g => g.GroupId, StringComparer.Ordinal)
            .ToList();

        var groupIds = orderedGroups.Select(g => g.GroupId).ToList();

        if (groupIds.Any(string.IsNullOrWhiteSpace) ||
            groupIds.Distinct().Count() != groupIds.Count)
        {
            throw new InvalidOperationException(
                "Group IDs must be unique and non-empty.");
        }

        var first = new List<string>();
        var second = new List<string>();

        foreach (var group in orderedGroups)
        {
            if (!rankedTeams.TryGetValue(group.GroupId, out var ranked) ||
                ranked.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Not enough ranked teams in {group.GroupId}.");
            }

            var qualified = ranked.Take(2).ToList();

            if (qualified.Any(string.IsNullOrWhiteSpace) ||
                qualified.Distinct().Count() != 2 ||
                qualified.Any(id => !group.TeamIds.Contains(id)))
            {
                throw new InvalidOperationException(
                    $"Invalid qualified teams in {group.GroupId}.");
            }

            first.Add(qualified[0]);
            second.Add(qualified[1]);
        }

        var all = first.Concat(second).ToList();

        if (all.Distinct().Count() != all.Count)
            throw new InvalidOperationException(
                "A team cannot qualify from multiple groups.");

        int count = groups.Count;
        var slots = new List<string>(count * 2);

        for (int i = 0; i < count; i++)
        {
            slots.Add(first[i]);
            slots.Add(second[(i + 1) % count]);
        }

        return slots;
    }
}