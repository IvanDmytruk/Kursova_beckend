
using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public class ThresholdSwissGenerator
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
        var participants = tournament.TournamentParticipants.ToList();

        if (participants.Count < 4)
            throw new InvalidOperationException(
                "Threshold Swiss requires at least 4 teams");

        if (participants.Count > 16)
            throw new NotSupportedException(
                "This Swiss pairing algorithm supports up to 16 teams");

        if (participants.Select(t => t.Id).Distinct().Count()
            != participants.Count)
            throw new InvalidOperationException(
                "Duplicate participants are not allowed");

        if (settings.WinsToAdvance < 1 ||
            settings.LossesToEliminate < 1)
            throw new InvalidOperationException(
                "Swiss thresholds must be positive");

        if (settings.AllowDraws)
            throw new InvalidOperationException(
                "Draws are not supported by this Swiss generator");

        if (existingMatches.Any(m =>
            m.Status != MatchStatus.Completed ||
            !m.HomeScore.HasValue ||
            !m.AwayScore.HasValue))
            throw new InvalidOperationException(
                "Complete all existing matches before generating a new round");

        if (existingMatches.Any(m =>
            m.HomeScore == m.AwayScore))
            throw new InvalidOperationException(
                "Swiss does not support drawn matches");

        var records = participants.ToDictionary(
            t => t.Id,
            t => new SwissRecord());

        var previousPairs = new HashSet<string>();

        foreach (var match in existingMatches)
        {
            if (!records.TryGetValue(
                    match.HomeTeamId, out var home) ||
                !records.TryGetValue(
                    match.AwayTeamId, out var away))
            {
                throw new InvalidOperationException(
                    "Match contains an unknown participant");
            }

            if (match.HomeTeamId == match.AwayTeamId)
                throw new InvalidOperationException(
                    "A team cannot play against itself");

            if (match.HomeScore!.Value >
                match.AwayScore!.Value)
            {
                home.Wins++;
                away.Losses++;
            }
            else
            {
                away.Wins++;
                home.Losses++;
            }

            previousPairs.Add(
                SwissPairHelper.PairKey(match.HomeTeamId, match.AwayTeamId));
        }

        foreach (var bye in previousByes)
        {
            if (bye.TournamentId != tournament.Id ||
                !records.TryGetValue(
                    bye.TeamId, out var record))
            {
                throw new InvalidOperationException(
                    "Invalid Swiss BYE record");
            }

            if (bye.CountsAsWin)
                record.Wins++;
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

        var active = participants
            .Where(t =>
                records[t.Id].Wins <
                    settings.WinsToAdvance &&
                records[t.Id].Losses <
                    settings.LossesToEliminate)
            .OrderByDescending(t => records[t.Id].Wins)
            .ThenBy(t => records[t.Id].Losses)
            .ThenBy(t => t.Id)
            .ToList();

        if (active.Count == 0)
            throw new InvalidOperationException(
                "Swiss stage is already completed");

        SwissBye? newBye = null;

        List<(Team Home, Team Away)>? bestPairs = null;

        if (active.Count % 2 != 0)
        {
            if (!settings.ByeCountsAsWin)
            {
                throw new InvalidOperationException(
                    "Threshold Swiss requires ByeCountsAsWin=true " +
                    "when the active team count is odd.");
            }

            var byeCandidates =
                SwissByeSelector.GetCandidates(
                    active,
                    previousByes,
                    team =>
                        records[team.Id].Wins -
                        records[team.Id].Losses);

            foreach (var candidate in byeCandidates)
            {
                var remaining = active
                    .Where(t => t.Id != candidate.Id)
                    .ToList();

                var pairs = FindBestPairings(
                    remaining,
                    records,
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
                    CountsAsWin = true
                };

                break;
            }

            if (bestPairs == null)
            {
                throw new InvalidOperationException(
                    "No valid Swiss pairings found for any BYE candidate. " +
                    "Consider allowing rematches or changing tournament rules.");
            }
        }
        else
        {
            bestPairs = FindBestPairings(
                active,
                records,
                previousPairs,
                settings.AllowRematches);

            if (bestPairs == null)
            {
                throw new InvalidOperationException(
                    "No valid Swiss pairings found. " +
                    "Check rematch settings or tournament rules.");
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
    private static List<(Team Home, Team Away)>?
        FindBestPairings(
            List<Team> teams,
            Dictionary<string, SwissRecord> records,
            HashSet<string> previousPairs,
            bool allowRematches)
    {
        List<(Team Home, Team Away)>? bestPairs = null;

        int bestRecordCost = int.MaxValue;
        int bestRematchCost = int.MaxValue;

        var currentPairs =
            new List<(Team Home, Team Away)>();

        void Search(
            List<Team> remaining,
            int recordCost,
            int rematchCost)
        {
            if (recordCost > bestRecordCost)
                return;

            if (recordCost == bestRecordCost &&
                rematchCost >= bestRematchCost)
                return;

            if (remaining.Count == 0)
            {
                bestRecordCost = recordCost;
                bestRematchCost = rematchCost;
                bestPairs = currentPairs.ToList();
                return;
            }

            var first = remaining[0];
            var firstRecord = records[first.Id];

            var candidates = remaining.Skip(1)
                .Select(opponent =>
                {
                    var opponentRecord =
                        records[opponent.Id];

                    int difference =
                        Math.Abs(
                            firstRecord.Wins -
                            opponentRecord.Wins) +
                        Math.Abs(
                            firstRecord.Losses -
                            opponentRecord.Losses);

                    bool isRematch =
                        previousPairs.Contains(SwissPairHelper.PairKey(first.Id, opponent.Id));

                    return new
                    {
                        Team = opponent,
                        Difference = difference,
                        IsRematch = isRematch
                    };
                })
                .Where(c =>
                    allowRematches || !c.IsRematch)
                .OrderBy(c => c.Difference)
                .ThenBy(c => c.IsRematch)
                .ThenBy(c => c.Team.Id)
                .ToList();

            foreach (var candidate in candidates)
            {
                int newRecordCost =
                    recordCost + candidate.Difference;

                int newRematchCost =
                    rematchCost +
                    (candidate.IsRematch ? 1 : 0);

                if (newRecordCost > bestRecordCost)
                    continue;

                if (newRecordCost == bestRecordCost &&
                    newRematchCost >= bestRematchCost)
                    continue;

                currentPairs.Add(
                    (first, candidate.Team));

                var next = remaining
                    .Where(t =>
                        t.Id != first.Id &&
                        t.Id != candidate.Team.Id)
                    .ToList();

                Search(
                    next,
                    newRecordCost,
                    newRematchCost);

                currentPairs.RemoveAt(
                    currentPairs.Count - 1);
            }
        }

        Search(teams, 0, 0);

        return bestPairs;
    }
    private sealed class SwissRecord
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
    }
}
