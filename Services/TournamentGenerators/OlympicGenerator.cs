using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public class OlympicGenerator
{
    public List<string?> CreateSlots(
        IReadOnlyList<Team> participants,
        bool shuffle)
    {
        if (participants.Count < 2)
            throw new ArgumentException(
                "At least 2 participants are required.");

        var ids = participants.Select(x => x.Id).ToList();

        if (ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct().Count() != ids.Count)
        {
            throw new ArgumentException(
                "Participant IDs must be unique and non-empty.");
        }

        if (shuffle)
        {
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
        }

        int bracketSize = 1;

        while (bracketSize < ids.Count)
            bracketSize *= 2;

        var seedOrder = BuildSeedOrder(bracketSize);

        return seedOrder
            .Select(seed =>
                seed <= ids.Count ? ids[seed - 1] : null)
            .ToList();
    }

    private static List<int> BuildSeedOrder(int size)
    {
        var seeds = new List<int> { 1 };

        for (int currentSize = 2;
             currentSize <= size;
             currentSize *= 2)
        {
            var next = new List<int>();

            foreach (int seed in seeds)
            {
                next.Add(seed);
                next.Add(currentSize + 1 - seed);
            }

            seeds = next;
        }

        return seeds;
    }

    public List<Match> GenerateFirstRound(
        Tournament tournament,
        IReadOnlyList<string?> slots,
        string stageId)
    {
        if (slots.Count < 2 ||
            (slots.Count & (slots.Count - 1)) != 0)
        {
            throw new ArgumentException(
                "Bracket size must be a power of two.");
        }

        var matches = new List<Match>();

        for (int i = 0; i < slots.Count; i += 2)
        {
            string? home = slots[i];
            string? away = slots[i + 1];


            if (home == null || away == null)
                continue;

            matches.Add(new Match
            {
                TournamentId = tournament.Id,
                StageId = stageId,
                MatchName =
                    $"{tournament.TournamentName} - " +
                    $"Olympic R1 M{i / 2 + 1}",
                HomeTeamId = home,
                AwayTeamId = away,
                SportName = tournament.SportName,
                RoundNumber = 1,
                BracketPosition = i / 2 + 1,
                IsAutomatic = true
            });
        }

        return matches;
    }
}
