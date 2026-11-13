using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public class Cs2SwissGenerator
{
    public SwissRoundGenerationResult Generate(
        Tournament tournament,
        List<Match> existingMatches,
        List<SwissBye> previousByes)
    {
        if (tournament.TournamentType != TournamentType.Cs2SwissSystem)
            throw new InvalidOperationException(
                "Tournament must use Cs2SwissSystem");

        var settings = tournament.Settings ?? new TournamentSettings();
        var participants = tournament.TournamentParticipants.ToList();

        if (participants.Count < 2)
            throw new InvalidOperationException(
                "At least 2 teams are required");

        if (participants.Any(t => string.IsNullOrWhiteSpace(t.Id)) ||
            participants.Select(t => t.Id).Distinct().Count() !=
            participants.Count)
            throw new InvalidOperationException(
                "Invalid or duplicate participants");

        if (settings.WinsToAdvance < 1 ||
            settings.LossesToEliminate < 1)
            throw new InvalidOperationException(
                "Win/loss thresholds must be positive");

        if (settings.AllowDraws)
            throw new InvalidOperationException(
                "CS2 Swiss does not allow draws");

        if (existingMatches.Any(m =>
            m.TournamentId != tournament.Id ||
            m.Status != MatchStatus.Completed ||
            !m.HomeScore.HasValue ||
            !m.AwayScore.HasValue ||
            m.HomeScore < 0 ||
            m.AwayScore < 0 ||
            m.HomeScore == m.AwayScore))
            throw new InvalidOperationException(
                "Invalid or incomplete previous matches");

        var records = participants.ToDictionary(
            t => t.Id,
            _ => (Wins: 0, Losses: 0));

        var previousPairs = new HashSet<string>();

        foreach (var match in existingMatches
            .OrderBy(m => m.RoundNumber))
        {
            if (!records.ContainsKey(match.HomeTeamId) ||
                !records.ContainsKey(match.AwayTeamId) ||
                match.HomeTeamId == match.AwayTeamId)
                throw new InvalidOperationException(
                    "Invalid match participants");

            var home = records[match.HomeTeamId];
            var away = records[match.AwayTeamId];

            if (home.Wins >= settings.WinsToAdvance ||
                home.Losses >= settings.LossesToEliminate ||
                away.Wins >= settings.WinsToAdvance ||
                away.Losses >= settings.LossesToEliminate)
                throw new InvalidOperationException(
                    "A qualified or eliminated team played again");

            if (match.HomeScore > match.AwayScore)
            {
                home.Wins++;
                away.Losses++;
            }
            else
            {
                away.Wins++;
                home.Losses++;
            }

            records[match.HomeTeamId] = home;
            records[match.AwayTeamId] = away;

            previousPairs.Add(
                SwissPairHelper.PairKey(
                    match.HomeTeamId,
                    match.AwayTeamId));
        }

        foreach (var bye in previousByes)
        {
            if (bye.TournamentId != tournament.Id ||
                !records.ContainsKey(bye.TeamId) ||
                bye.RoundNumber < 1)
            {
                throw new InvalidOperationException(
                    "Invalid previous CS2 Swiss BYE");
            }

            if (bye.CountsAsWin)
            {
                var record = records[bye.TeamId];
                record.Wins++;
                records[bye.TeamId] = record;
            }
        }

        int nextRound = existingMatches.Count == 0
            ? 1
            : existingMatches.Max(m => m.RoundNumber) + 1;

        var active = participants
            .Where(t =>
                records[t.Id].Wins < settings.WinsToAdvance &&
                records[t.Id].Losses < settings.LossesToEliminate)
            .OrderByDescending(t => records[t.Id].Wins)
            .ThenBy(t => records[t.Id].Losses)
            .ThenBy(t => t.Id)
            .ToList();

        if (active.Count == 0)
            throw new InvalidOperationException(
                "Swiss stage is completed");

        SwissBye? newBye = null;

        if (active.Count % 2 != 0)
        {
            var byeCounts = previousByes
                .GroupBy(b => b.TeamId)
                .ToDictionary(g => g.Key, g => g.Count());

            var byeTeam = active
                .OrderBy(t => byeCounts.GetValueOrDefault(t.Id, 0))
                .ThenBy(t => records[t.Id].Wins)
                .ThenByDescending(t => records[t.Id].Losses)
                .ThenBy(t => t.Id)
                .First();

            newBye = new SwissBye
            {
                TournamentId = tournament.Id,
                TeamId = byeTeam.Id,
                RoundNumber = nextRound,
                PointsAwarded = settings.PointsForBye,
                CountsAsWin = settings.ByeCountsAsWin
            };

            active.Remove(byeTeam);
        }

        var pairs = FindAdaptivePairs(
            active,
            records,
            previousPairs,
            settings.AllowRematches);

        if (pairs == null)
            throw new InvalidOperationException(
                "Cannot generate valid CS2 Swiss pairings");

        var matches = new List<Match>();

        foreach (var (home, away) in pairs)
        {
            matches.Add(new Match
            {
                TournamentId = tournament.Id,
                MatchName = $"{home.TeamName} vs {away.TeamName}",
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                SportName = tournament.SportName,
                RoundNumber = nextRound,
                Status = MatchStatus.Planing,
                IsAutomatic = true,
                StartTime = null,
                TicketCost = 0,
                MaxViewers = 0
            });
        }
        return new SwissRoundGenerationResult
        {
            Matches = matches,
            Bye = newBye
        };
    }

    private static List<(Team Home, Team Away)>? FindAdaptivePairs(
        List<Team> teams,
        Dictionary<string, (int Wins, int Losses)> records,
        HashSet<string> previousPairs,
        bool allowRematches)
    {
        var memo = new Dictionary<string, PairingSolution?>();

        PairingSolution? Solve(List<Team> remaining)
        {
            if (remaining.Count == 0)
                return new PairingSolution(
                    new List<(Team Home, Team Away)>(),
                    0);

            string key = string.Join("|",
                remaining.Select(t => t.Id).OrderBy(id => id));

            if (memo.TryGetValue(key, out var cached))
                return cached;

            var first = remaining[0];
            var firstRecord = records[first.Id];

            PairingSolution? best = null;

            var candidates = remaining
                .Skip(1)
                .Where(opponent =>
                    allowRematches ||
                    !previousPairs.Contains(
                        SwissPairHelper.PairKey(
                            first.Id,
                            opponent.Id)))
                .OrderBy(opponent =>
                    PairingCost(
                        firstRecord,
                        records[opponent.Id]))
                .ThenBy(opponent => opponent.Id);

            foreach (var opponent in candidates)
            {
                int cost = PairingCost(
                    firstRecord,
                    records[opponent.Id]);

                if (best != null && cost >= best.Cost)
                    continue;

                var rest = remaining
                    .Where(t =>
                        t.Id != first.Id &&
                        t.Id != opponent.Id)
                    .ToList();

                var solution = Solve(rest);

                if (solution == null)
                    continue;

                int totalCost = cost + solution.Cost;

                if (best == null || totalCost < best.Cost)
                {
                    var pairs =
                        new List<(Team Home, Team Away)>
                        {
                            (first, opponent)
                        };

                    pairs.AddRange(solution.Pairs);

                    best = new PairingSolution(
                        pairs,
                        totalCost);
                }

                if (best.Cost == 0)
                    break;
            }

            memo[key] = best;
            return best;
        }

        return Solve(teams)?.Pairs;
    }

    private static int PairingCost(
        (int Wins, int Losses) first,
        (int Wins, int Losses) second)
    {
        if (first == second)
            return 0;

        int distance =
            Math.Abs(first.Wins - second.Wins) +
            Math.Abs(first.Losses - second.Losses);
        return 1000 + distance;
    }

    private sealed record PairingSolution(
        List<(Team Home, Team Away)> Pairs,
        int Cost);
}
