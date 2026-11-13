using Beckend.DTOs.Standings;
using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentStandings
{
    public class RoundRobinStandingsGenerator : IStandingsGenerator
    {
        public TournamentType SupportedType =>
            TournamentType.RoundRobin;

        public TournamentStandingsDto Generate(
            Tournament tournament,
            List<Match> matches)
        {
            var entries = tournament.TournamentParticipants
                .Select(team => new StandingsEntryDto
                {
                    ParticipantId = team.Id,
                    ParticipantName = team.TeamName
                })
                .ToDictionary(x => x.ParticipantId);

            var settings = tournament.Settings ?? new TournamentSettings();

            if (settings.PointsForWin < 0 ||
                settings.PointsForDraw < 0 ||
                settings.PointsForLoss < 0)
            {
                throw new InvalidOperationException(
                    "Standings points cannot be negative.");
            }

            var completedMatches = matches
            .Where(m => m.Status == MatchStatus.Completed).ToList();

            if (completedMatches.Any(m =>
                !m.HomeScore.HasValue ||
                !m.AwayScore.HasValue))
            {
                throw new InvalidOperationException(
                    "Completed match must have both scores.");
            }

            foreach (var match in completedMatches)
            {

                if (!entries.TryGetValue(match.HomeTeamId, out var home) || !entries.TryGetValue(match.AwayTeamId, out var away))
                {
                    throw new InvalidOperationException(
                        "Match contains participants outside the tournament.");
                }
                if (match.TournamentId != tournament.Id)
                {
                    throw new InvalidOperationException(
                        "Match belongs to another tournament.");
                }
                if (match.HomeTeamId == match.AwayTeamId)
                {
                    throw new InvalidOperationException(
                        "A team cannot play against itself.");
                }

                int homeScore = match.HomeScore!.Value;
                int awayScore = match.AwayScore!.Value;

                home.Played++;
                away.Played++;

                home.ScoreFor += homeScore;
                home.ScoreAgainst += awayScore;

                away.ScoreFor += awayScore;
                away.ScoreAgainst += homeScore;


                if (homeScore > awayScore)
                {
                    home.Wins++;
                    away.Losses++;

                    home.Points += settings.PointsForWin;
                    away.Points += settings.PointsForLoss;
                }
                else if (awayScore > homeScore)
                {
                    away.Wins++;
                    home.Losses++;

                    away.Points += settings.PointsForWin;
                    home.Points += settings.PointsForLoss;
                }
                else
                {
                    home.Draws++;
                    away.Draws++;

                    home.Points += settings.PointsForDraw;
                    away.Points += settings.PointsForDraw;
                }

            }

            var sorted = entries.Values
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.ScoreDifference)
                .ThenByDescending(x => x.ScoreFor)
                .ThenBy(x => x.ParticipantName)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
                sorted[i].Position = i + 1;

            return new TournamentStandingsDto
            {
                TournamentId = tournament.Id,
                TournamentType = tournament.TournamentType.ToString(),
                ViewType = "Table",
                Sections = new List<StandingsSectionDto>
                {
                    new StandingsSectionDto
                    {
                        SectionId = "overall",
                        SectionName = "Overall Standings",
                        Entries = sorted
                    }
                }
            };
        }
    }
}
