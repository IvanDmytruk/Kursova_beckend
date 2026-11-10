
using Beckend.Models.TournamentPlanning;
using Beckend.Services.Interfaces;

namespace Beckend.Services.TournamentPlanning;

public class SwissStageSimulator : ISwissStageSimulator
{
    public SwissSimulationResult Simulate(
        int participantCount,
        int winsToAdvance,
        int lossesToEliminate)
    {
        var result = new SwissSimulationResult();

        if (participantCount < 2 ||
            participantCount % 2 != 0 ||
            winsToAdvance < 1 ||
            lossesToEliminate < 1)
        {
            result.Warnings.Add(
                "Invalid participant count or Swiss thresholds.");
            return result;
        }

        var groups = new Dictionary<(int Wins, int Losses), int>
        {
            [(0, 0)] = participantCount
        };

        int advanced = 0;
        int eliminated = 0;
        int totalMatches = 0;
        int roundNumber = 0;

        while (groups.Count > 0)
        {
            roundNumber++;

            var next = new Dictionary<(int Wins, int Losses), int>();
            int roundMatches = 0;

            foreach (var group in groups)
            {
                var (wins, losses) = group.Key;
                int count = group.Value;

                if (count % 2 != 0)
                {
                    result.Warnings.Add(
                        $"Round {roundNumber}: record " +
                        $"{wins}-{losses} has {count} teams. " +
                        "Cross-record pairing or bye is required.");

                    return result;
                }

                int pairs = count / 2;
                roundMatches += pairs;

                AddResult(wins + 1, losses, pairs);
                AddResult(wins, losses + 1, pairs);
            }

            totalMatches += roundMatches;

            result.Rounds.Add(new SwissRoundSnapshot
            {
                RoundNumber = roundNumber,
                MatchesPlayed = roundMatches,
                AdvancedCount = advanced,
                EliminatedCount = eliminated,
                ActiveGroups = next
                    .OrderByDescending(x => x.Key.Wins)
                    .ThenBy(x => x.Key.Losses)
                    .Select(x => new SwissRecordGroup
                    {
                        Wins = x.Key.Wins,
                        Losses = x.Key.Losses,
                        TeamCount = x.Value
                    })
                    .ToList()
            });

            groups = next;

            void AddResult(int wins, int losses, int count)
            {
                if (wins >= winsToAdvance)
                {
                    advanced += count;
                }
                else if (losses >= lossesToEliminate)
                {
                    eliminated += count;
                }
                else
                {
                    var key = (wins, losses);
                    next.TryGetValue(key, out int existing);
                    next[key] = existing + count;
                }
            }
        }

        result.IsValid = advanced + eliminated == participantCount;
        result.AdvancedCount = advanced;
        result.EliminatedCount = eliminated;
        result.TotalMatches = totalMatches;
        result.TotalRounds = roundNumber;

        return result;
    }
}
