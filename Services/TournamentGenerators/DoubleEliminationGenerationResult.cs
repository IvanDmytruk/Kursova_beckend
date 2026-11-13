
using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public sealed class DoubleEliminationGenerationResult
{
    public List<Match> Matches { get; set; } = new();
    public List<string> ByeTeamIds { get; set; } = new();
    public string? ChampionId { get; set; }
    public bool IsCompleted => ChampionId != null;
}

public sealed class DoubleEliminationGenerator
{
    public DoubleEliminationGenerationResult Generate(
        Tournament tournament,
        IReadOnlyList<Match> existingMatches,
        IReadOnlyList<string> previousByeTeamIds)
    {
        ArgumentNullException.ThrowIfNull(tournament);
        ArgumentNullException.ThrowIfNull(existingMatches);
        ArgumentNullException.ThrowIfNull(previousByeTeamIds);

        if (tournament.TournamentType != TournamentType.DoubleElimination)
            throw new InvalidOperationException(
                "Tournament must use DoubleElimination format.");

        var participants = tournament.TournamentParticipants;

        if (participants == null || participants.Count < 2)
            throw new ArgumentException(
                "At least two participants are required.");

        var ids = participants.Select(t => t.Id).ToList();

        if (ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct().Count() != ids.Count)
        {
            throw new ArgumentException(
                "Participant IDs must be unique and non-empty.");
        }

        var records = ids.ToDictionary(
            id => id,
            id => new TeamRecord(id));

        var matches = existingMatches
            .Where(m => m.TournamentId == tournament.Id &&
                        m.IsAutomatic &&
                        (m.StageId == "double-main" ||
                         m.StageId == "grand-final"))
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.BracketPosition)
            .ToList();

        if (matches.Any(m => m.Status != MatchStatus.Completed))
            throw new InvalidOperationException(
                "Complete all generated matches before generating the next round.");

        foreach (var match in matches)
        {
            if (!records.ContainsKey(match.HomeTeamId) ||
                !records.ContainsKey(match.AwayTeamId) ||
                match.HomeTeamId == match.AwayTeamId ||
                !match.HomeScore.HasValue ||
                !match.AwayScore.HasValue ||
                match.HomeScore < 0 ||
                match.AwayScore < 0 ||
                match.HomeScore == match.AwayScore)
            {
                throw new InvalidOperationException(
                    "Invalid DoubleElimination match result.");
            }

            var winnerId = match.HomeScore > match.AwayScore
                ? match.HomeTeamId
                : match.AwayTeamId;

            var loserId = winnerId == match.HomeTeamId
                ? match.AwayTeamId
                : match.HomeTeamId;

            if (records[winnerId].Losses >= 2 ||
                records[loserId].Losses >= 2)
            {
                throw new InvalidOperationException(
                    "An eliminated team cannot play another match.");
            }

            records[winnerId].Wins++;
            records[loserId].Losses++;
        }

        var active = records.Values
            .Where(r => r.Losses < 2)
            .ToList();

        var result = new DoubleEliminationGenerationResult();

        if (active.Count == 1)
        {
            result.ChampionId = active[0].TeamId;
            return result;
        }

        if (active.Count == 0)
            throw new InvalidOperationException(
                "No active teams remain.");

        int nextRound = matches.Count == 0
            ? 1
            : matches.Max(m => m.RoundNumber) + 1;

        // Коли залишилися дві команди, граємо фінал.
        // Якщо після першого фіналу обидві мають
        // по одній поразці, потрібен Grand Final Reset.
        if (active.Count == 2 && matches.Count > 0)
        {
            var finalists = active
                .OrderBy(r => r.Losses)
                .ThenByDescending(r => r.Wins)
                .ThenBy(r => r.TeamId)
                .ToList();

            result.Matches.Add(CreateMatch(
                tournament,
                finalists[0].TeamId,
                finalists[1].TeamId,
                nextRound,
                1,
                "grand-final"));

            return result;
        }

        var previousPairs = matches
            .Select(m => PairKey(m.HomeTeamId, m.AwayTeamId))
            .ToHashSet();

        var previousByes = previousByeTeamIds.ToHashSet();

        var remaining = active
            .OrderBy(r => r.TeamId)
            .ToList();

        // Перемішування лише перед першим раундом.
        if (matches.Count == 0 &&
            tournament.Settings?.ShuffleParticipants == true)
        {
            for (int i = remaining.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);

                (remaining[i], remaining[j]) =
                    (remaining[j], remaining[i]);
            }
        }

        // Перший раунд: усі команди мають 0-0.
        // Для непарної кількості одразу визначаємо BYE.
        if (matches.Count == 0 && remaining.Count % 2 != 0)
        {
            var bye = remaining.FirstOrDefault(
                t => !previousByes.Contains(t.TeamId))
                ?? remaining[0];

            result.ByeTeamIds.Add(bye.TeamId);
            remaining.Remove(bye);
        }

        // Наступні раунди:
        // якщо команд непарна кількість, вибираємо BYE
        // з урахуванням попередніх пропусків.
        if (matches.Count > 0 && remaining.Count % 2 != 0)
        {
            var bye = SelectBye(
                remaining,
                previousByes);

            result.ByeTeamIds.Add(bye.TeamId);
            remaining.Remove(bye);
        }

        int position = 1;

        while (remaining.Count >= 2)
        {
            var pair = FindBestPair(
                remaining,
                previousPairs);

            var home = pair.Home;
            var away = pair.Away;

            result.Matches.Add(CreateMatch(
                tournament,
                home.TeamId,
                away.TeamId,
                nextRound,
                position++,
                "double-main"));

            remaining.Remove(home);
            remaining.Remove(away);
        }

        if (remaining.Count != 0)
            throw new InvalidOperationException(
                "Pairing algorithm left an unmatched team.");

        return result;
    }

    private static TeamRecord SelectBye(
        List<TeamRecord> teams,
        HashSet<string> previousByes)
    {
        return teams
            .Select(team => new
            {
                Team = team,

                SameRecordPairs = teams
                    .Where(t => t.TeamId != team.TeamId)
                    .GroupBy(t => new
                    {
                        t.Wins,
                        t.Losses
                    })
                    .Sum(g => g.Count() / 2),

                HadBye = previousByes.Contains(team.TeamId)
            })

            .OrderByDescending(x => x.SameRecordPairs)

            .ThenBy(x => x.HadBye ? 1 : 0)

            .ThenBy(x => x.Team.Wins)
            .ThenByDescending(x => x.Team.Losses)
            .ThenBy(x => x.Team.TeamId)
            .First()
            .Team;
    }


    private static (TeamRecord Home, TeamRecord Away)
        FindBestPair(
            List<TeamRecord> teams,
            HashSet<string> previousPairs)
    {
        TeamRecord? bestHome = null;
        TeamRecord? bestAway = null;

        (int, int, int, int)? bestScore = null;

        for (int i = 0; i < teams.Count; i++)
        {
            for (int j = i + 1; j < teams.Count; j++)
            {
                var home = teams[i];
                var away = teams[j];

                bool sameRecord =
                    home.Wins == away.Wins &&
                    home.Losses == away.Losses;

                bool alreadyPlayed = previousPairs.Contains(
                    PairKey(home.TeamId, away.TeamId));

                int lossDifference =
                    Math.Abs(home.Losses - away.Losses);

                int winDifference =
                    Math.Abs(home.Wins - away.Wins);

                // Пріоритети:
                // 1. Однаковий баланс.
                // 2. Відсутність попередньої зустрічі.
                // 3. Найменша різниця поразок.
                // 4. Найменша різниця перемог.
                var score = (
                    sameRecord ? 0 : 1,
                    alreadyPlayed ? 1 : 0,
                    lossDifference,
                    winDifference
                );

                if (bestScore == null ||
                    score.CompareTo(bestScore.Value) < 0)
                {
                    bestScore = score;
                    bestHome = home;
                    bestAway = away;
                }
            }
        }

        if (bestHome == null || bestAway == null)
            throw new InvalidOperationException(
                "Unable to create a valid pairing.");

        return (bestHome, bestAway);
    }

    private static string PairKey(string first, string second)
    {
        return string.CompareOrdinal(first, second) < 0
            ? $"{first}|{second}"
            : $"{second}|{first}";
    }

    private static Match CreateMatch(
        Tournament tournament,
        string homeId,
        string awayId,
        int round,
        int position,
        string stageId)
    {
        return new Match
        {
            TournamentId = tournament.Id,
            HomeTeamId = homeId,
            AwayTeamId = awayId,
            SportName = tournament.SportName,
            StageId = stageId,
            RoundNumber = round,
            BracketPosition = position,
            IsAutomatic = true,
            MatchName =
                $"{tournament.TournamentName} - " +
                $"{stageId} R{round} M{position}"
        };
    }

    private sealed class TeamRecord
    {
        public string TeamId { get; }
        public int Wins { get; set; }
        public int Losses { get; set; }

        public TeamRecord(string teamId)
        {
            TeamId = teamId;
        }
    }
}
