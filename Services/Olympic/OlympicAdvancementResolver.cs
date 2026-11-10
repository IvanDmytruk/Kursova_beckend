using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;

namespace Beckend.Services.Olympic;

public static class OlympicAdvancementResolver
{
    public static List<string?> Resolve(
        OlympicBracketState state,
        List<Match> matches,
        int round)
    {
        if (state.BracketSize < 2 ||
            (state.BracketSize & (state.BracketSize - 1)) != 0 ||
            state.Slots == null ||
            state.Slots.Count != state.BracketSize)
        {
            throw new InvalidOperationException(
                "Invalid saved Olympic bracket.");
        }

        int totalRounds = 0;
        int size = state.BracketSize;

        while (size > 1)
        {
            size /= 2;
            totalRounds++;
        }

        if (round < 1 || round > totalRounds)
            throw new InvalidOperationException(
                "Invalid round number.");

        int positions = state.BracketSize >> round;

        var winners = new string?[positions];
        var seenPositions = new HashSet<int>();

        foreach (var match in matches)
        {
            int index = match.BracketPosition - 1;

            if (index < 0 || index >= positions ||
                !seenPositions.Add(index))
            {
                throw new InvalidOperationException(
                    "Invalid or duplicate bracket position.");
            }

            if (match.Status != MatchStatus.Completed)
                throw new InvalidOperationException(
                    $"Match {match.Id} is not completed.");

            if (string.IsNullOrWhiteSpace(match.WinnerId) ||
                (match.WinnerId != match.HomeTeamId &&
                 match.WinnerId != match.AwayTeamId))
            {
                throw new InvalidOperationException(
                    $"Match {match.Id} has no valid winner.");
            }

            winners[index] = match.WinnerId;
        }

        if (round == 1)
        {
            for (int position = 0;
                 position < positions;
                 position++)
            {
                string? first = state.Slots[position * 2];
                string? second = state.Slots[position * 2 + 1];

                if (first == null && second == null)
                    throw new InvalidOperationException(
                        "Two empty slots in the same first-round pairing.");

                if (first != null && second != null)
                {
                    if (!seenPositions.Contains(position))
                        throw new InvalidOperationException(
                            $"Missing first-round match at position {position + 1}.");

                    var match = matches.Single(m =>
                        m.BracketPosition == position + 1);

                    bool sameParticipants =
                        (match.HomeTeamId == first &&
                         match.AwayTeamId == second) ||
                        (match.HomeTeamId == second &&
                         match.AwayTeamId == first);

                    if (!sameParticipants)
                        throw new InvalidOperationException(
                            "Match participants do not match saved bracket.");
                }
                else
                {
                    if (seenPositions.Contains(position))
                        throw new InvalidOperationException(
                            "A BYE position must not contain a match.");

                    winners[position] = first ?? second;
                }
            }
        }
        else
        {
            if (seenPositions.Count != positions)
                throw new InvalidOperationException(
                    "Some matches of the current round are missing.");
        }

        if (winners.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Not all advancing teams could be determined.");

        if (winners.Distinct().Count() != winners.Length)
            throw new InvalidOperationException(
                "Duplicate advancing teams detected.");

        return winners.ToList();
    }
}
