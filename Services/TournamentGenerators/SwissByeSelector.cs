using Beckend.Models;
namespace Beckend.Services.TournamentGenerators;
public static class SwissByeSelector
{
    public static List<Team> GetCandidates(
        List<Team> activeTeams,
        List<SwissBye> previousByes,
        Func<Team, decimal> getScore)
    {
        if (activeTeams == null || activeTeams.Count == 0)
        {
            throw new InvalidOperationException(
                "No active teams available for BYE");
        }

        if (previousByes == null)
        {
            throw new ArgumentNullException(nameof(previousByes));
        }

        if (getScore == null)
        {
            throw new ArgumentNullException(nameof(getScore));
        }

        if (activeTeams.Select(t => t.Id).Distinct().Count()
            != activeTeams.Count)
        {
            throw new InvalidOperationException(
                "Duplicate active team IDs");
        }

        var byeCounts = previousByes
            .GroupBy(b => b.TeamId)
            .ToDictionary(
                g => g.Key,
                g => g.Count());

        return activeTeams
            .OrderBy(t =>
                byeCounts.GetValueOrDefault(t.Id))
            .ThenBy(getScore)
            .ThenBy(t => t.Id)
            .ToList();
    }
    public static Team Select(
        List<Team> activeTeams,
        List<SwissBye> previousByes,
        Func<Team, decimal> getScore)
    {
        return GetCandidates(
            activeTeams,
            previousByes,
            getScore
        ).First();
    }
}
