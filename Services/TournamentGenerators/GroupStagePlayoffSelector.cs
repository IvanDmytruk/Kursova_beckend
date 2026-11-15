using Beckend.DTOs.Standings;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public static class GroupStagePlayoffSelector
{
    public static List<string> Select(
        Tournament tournament,
        List<TournamentGroup> groups,
        TournamentStandingsDto standings)
    {
        if (groups.Count == 0)
            throw new InvalidOperationException(
                "Tournament has no groups.");

        var selected = new List<string>();

        foreach (var group in groups.OrderBy(g => g.GroupId))
        {
            var section = standings.Sections.SingleOrDefault(
                s => s.SectionId == group.GroupId);

            if (section == null)
                throw new InvalidOperationException(
                    $"Standings for {group.GroupId} not found.");

            var entries = section.Entries;

            if (entries.Count != group.TeamIds.Count ||
                entries.Select(e => e.ParticipantId).Distinct().Count()
                    != group.TeamIds.Count ||
                entries.Any(e => !group.TeamIds.Contains(e.ParticipantId)))
            {
                throw new InvalidOperationException(
                    $"Invalid standings for {group.GroupId}.");
            }

            var qualified = entries
                .Where(e => !e.IsDisqualified)
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.ScoreDifference)
                .ThenByDescending(e => e.ScoreFor)
                .ThenBy(e => e.ParticipantName)
                .Take(group.AdvancingCount)
                .ToList();

            if (qualified.Count != group.AdvancingCount)
            {
                throw new InvalidOperationException(
                    $"Not enough active teams in {group.GroupId}.");
            }

            selected.AddRange(
                qualified.Select(e => e.ParticipantId));
        }

        if (selected.Count != selected.Distinct().Count())
            throw new InvalidOperationException(
                "Duplicate playoff participants.");

        if (selected.Count != tournament.Settings.ParticipantsAdvancing)
            throw new InvalidOperationException(
                "Playoff participant count is inconsistent.");

        return selected;
    }
}