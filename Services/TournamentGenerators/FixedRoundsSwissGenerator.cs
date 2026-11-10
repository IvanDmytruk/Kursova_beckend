using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public class FixedRoundsSwissGenerator
{
    public SwissRoundGenerationResult Generate(
        Tournament tournament,
        List<Match> existingMatches,
        List<SwissBye> previousByes)
    {
        if (tournament.TournamentType != TournamentType.SwissSystem)
            throw new InvalidOperationException(
                "Tournament must use SwissSystem");

        var settings = tournament.Settings ?? new TournamentSettings();
        var teams = tournament.TournamentParticipants.ToList();

        if (settings.RoundsCount < 1)
            throw new InvalidOperationException(
                "RoundsCount must be positive");

        if (teams.Count < 2 ||
            teams.Select(t => t.Id).Distinct().Count() != teams.Count)
        {
            throw new InvalidOperationException(
                "Invalid Swiss participants");
        }

        if (existingMatches.Any(m =>
            m.Status != MatchStatus.Completed ||
            !m.HomeScore.HasValue ||
            !m.AwayScore.HasValue))
        {
            throw new InvalidOperationException(
                "Complete the previous round first");
        }

        if (!settings.AllowDraws &&
            existingMatches.Any(m =>
                m.HomeScore == m.AwayScore))
        {
            throw new InvalidOperationException(
                "Draws are not allowed");
        }

        int lastMatchRound = existingMatches.Count == 0
            ? 0
            : existingMatches.Max(m => m.RoundNumber);

        int lastByeRound = previousByes.Count == 0
            ? 0
            : previousByes.Max(b => b.RoundNumber);

        int nextRound = Math.Max(
            lastMatchRound,
            lastByeRound) + 1;

        if (nextRound > settings.RoundsCount)
        {
            throw new InvalidOperationException(
                "All Swiss rounds have been completed");
        }
        var points = teams.ToDictionary(
            t => t.Id,
            _ => 0m);

        var previousPairs = new HashSet<string>();

        foreach (var match in existingMatches)
        {
            if (!points.ContainsKey(match.HomeTeamId) ||
                !points.ContainsKey(match.AwayTeamId) ||
                match.HomeTeamId == match.AwayTeamId)
            {
                throw new InvalidOperationException(
                    "Invalid match participants");
            }

            if (match.HomeScore > match.AwayScore)
            {
                points[match.HomeTeamId] +=
                    settings.PointsForWin;

                points[match.AwayTeamId] +=
                    settings.PointsForLoss;
            }
            else if (match.AwayScore > match.HomeScore)
            {
                points[match.AwayTeamId] +=
                    settings.PointsForWin;

                points[match.HomeTeamId] +=
                    settings.PointsForLoss;
            }
            else
            {
                points[match.HomeTeamId] +=
                    settings.PointsForDraw;

                points[match.AwayTeamId] +=
                    settings.PointsForDraw;
            }

            previousPairs.Add(
                PairKey(
                    match.HomeTeamId,
                    match.AwayTeamId));
        }
        foreach (var bye in previousByes)
        {
            if (bye.TournamentId != tournament.Id ||
                !points.ContainsKey(bye.TeamId))
            {
                throw new InvalidOperationException(
                    "Invalid Swiss BYE record");
            }

            points[bye.TeamId] += bye.PointsAwarded;
        }
        SwissBye? newBye = null;

        List<(Team Home, Team Away)>? bestPairs = null;

        if (teams.Count % 2 != 0)
        {
            var byeCandidates =
                SwissByeSelector.GetCandidates(
                    teams,
                    previousByes,
                    team => points[team.Id]);

            foreach (var candidate in byeCandidates)
            {
                var remaining = teams
                    .Where(t => t.Id != candidate.Id)
                    .OrderByDescending(t => points[t.Id])
                    .ThenBy(t => t.Id)
                    .ToList();

                var pairs = FindPairs(
                    remaining,
                    points,
                    previousPairs,
                    settings.AllowRematches);

                if (pairs == null)
                    continue;
                bestPairs = pairs;

                newBye = new SwissBye
                {
                    TournamentId = tournament.Id,
                    TeamId = candidate.Id,
                    RoundNumber = nextRound,
                    PointsAwarded = settings.PointsForBye,
                    CountsAsWin = false
                };

                break;
            }

            if (bestPairs == null)
            {
                throw new InvalidOperationException(
                    "No valid Swiss pairings found for any BYE candidate. " +
                    "Consider enabling rematches or revising tournament rules.");
            }
        }
        else
        {
            var ordered = teams
                .OrderByDescending(t => points[t.Id])
                .ThenBy(t => t.Id)
                .ToList();

            bestPairs = FindPairs(
                ordered,
                points,
                previousPairs,
                settings.AllowRematches);

            if (bestPairs == null)
            {
                throw new InvalidOperationException(
                    "No valid pairings. Enable rematches or revise rules.");
            }
        }
        var matches = bestPairs.Select(pair => new Match
        {
            MatchName =
                $"{pair.Home.TeamName} vs {pair.Away.TeamName}",

            TournamentId = tournament.Id,
            HomeTeamId = pair.Home.Id,
            AwayTeamId = pair.Away.Id,
            SportName = tournament.SportName,
            RoundNumber = nextRound,

            Status = MatchStatus.Planing,
            IsAutomatic = true,
            StartTime = null,
            TicketCost = 0,
            MaxViewers = 0
        }).ToList();

        return new SwissRoundGenerationResult
        {
            Matches = matches,
            Bye = newBye
        };
    }
    private static List<(Team Home, Team Away)>? FindPairs(
        List<Team> teams,
        Dictionary<string, decimal> points,
        HashSet<string> previousPairs,
        bool allowRematches)
    {
        List<(Team Home, Team Away)>? best = null;

        decimal bestCost = decimal.MaxValue;
        int bestRematches = int.MaxValue;

        var current =
            new List<(Team Home, Team Away)>();

        void Search(
            List<Team> remaining,
            decimal cost,
            int rematches)
        {
            if (cost > bestCost ||
                (cost == bestCost &&
                 rematches >= bestRematches))
            {
                return;
            }

            if (remaining.Count == 0)
            {
                bestCost = cost;
                bestRematches = rematches;
                best = current.ToList();
                return;
            }

            var first = remaining[0];

            var candidates = remaining
                .Skip(1)
                .Select(opponent => new
                {
                    Team = opponent,

                    Difference = Math.Abs(
                        points[first.Id] -
                        points[opponent.Id]),

                    IsRematch = previousPairs.Contains(
                        PairKey(first.Id, opponent.Id))
                })
                .Where(c =>
                    allowRematches || !c.IsRematch)
                .OrderBy(c => c.Difference)
                .ThenBy(c => c.IsRematch)
                .ThenBy(c => c.Team.Id)
                .ToList();

            foreach (var candidate in candidates)
            {
                decimal newCost =
                    cost + candidate.Difference;

                int newRematches =
                    rematches +
                    (candidate.IsRematch ? 1 : 0);

                if (newCost > bestCost ||
                    (newCost == bestCost &&
                     newRematches >= bestRematches))
                {
                    continue;
                }

                current.Add(
                    (first, candidate.Team));

                var next = remaining
                    .Where(t =>
                        t.Id != first.Id &&
                        t.Id != candidate.Team.Id)
                    .ToList();

                Search(
                    next,
                    newCost,
                    newRematches);

                current.RemoveAt(
                    current.Count - 1);
            }
        }

        Search(teams, 0m, 0);

        return best;
    }
    private static string PairKey(
        string a,
        string b)
    {
        return string.CompareOrdinal(a, b) < 0
            ? $"{a}:{b}"
            : $"{b}:{a}";
    }
}
