using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentAdministration;

public class DisqualificationPlanner
{
    public List<DisqualificationMatchDecision> BuildPlan(
        Tournament tournament,
        string disqualifiedTeamId,
        IReadOnlyCollection<Match> matches,
        IReadOnlyCollection<string> otherDisqualifiedTeamIds,
        int technicalWinnerScore = 1)
    {
        ArgumentNullException.ThrowIfNull(tournament);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(otherDisqualifiedTeamIds);

        if (string.IsNullOrWhiteSpace(disqualifiedTeamId))
            throw new ArgumentException(
                "Team ID is required.");

        if (technicalWinnerScore <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(technicalWinnerScore));

        if (!tournament.TournamentParticipants.Any(
            t => t.Id == disqualifiedTeamId))
        {
            throw new ArgumentException(
                "Team is not registered in this tournament.");
        }

        var excluded = otherDisqualifiedTeamIds.ToHashSet();
        excluded.Add(disqualifiedTeamId);

        bool elimination =
            tournament.TournamentType == TournamentType.OlympicSystem ||
            tournament.TournamentType == TournamentType.DoubleElimination;

        var result = new List<DisqualificationMatchDecision>();

        foreach (var match in matches
            .Where(m =>
                m.TournamentId == tournament.Id &&
                (m.HomeTeamId == disqualifiedTeamId ||
                 m.AwayTeamId == disqualifiedTeamId) &&
                m.Status != MatchStatus.Completed &&
                m.Status != MatchStatus.Cancelled)
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.StartTime))
        {
            if (match.HomeTeamId == match.AwayTeamId)
                throw new InvalidOperationException(
                    $"Match {match.Id} has identical teams.");

            string opponentId =
                match.HomeTeamId == disqualifiedTeamId
                    ? match.AwayTeamId
                    : match.HomeTeamId;

            if (string.IsNullOrWhiteSpace(opponentId))
                continue;

            if (excluded.Contains(opponentId))
                continue;

            bool homeWins = match.HomeTeamId == opponentId;

            result.Add(new DisqualificationMatchDecision
            {
                MatchId = match.Id,
                WinnerTeamId = opponentId,
                HomeScore = homeWins ? technicalWinnerScore : 0,
                AwayScore = homeWins ? 0 : technicalWinnerScore,
                AdvancesWinner = elimination
            });
        }

        return result;
    }
}
