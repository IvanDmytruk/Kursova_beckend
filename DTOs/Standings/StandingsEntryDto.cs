
namespace Beckend.DTOs.Standings
{
    public class StandingsEntryDto
    {
        public int Position { get; set; }

        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;

        public int Played { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }

        public int ScoreFor { get; set; }
        public int ScoreAgainst { get; set; }

        public int ScoreDifference => ScoreFor - ScoreAgainst;

        public decimal Points { get; set; }

        public string Record => $"{Wins}-{Losses}";

        public string Status { get; set; } = "Active";
    }
}
