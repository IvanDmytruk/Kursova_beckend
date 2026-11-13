namespace Beckend.DTOs
{
    public class MatchDto
    {
        public string Id { get; set; } = string.Empty;
        public string MatchName { get; set; } = string.Empty;

        public double TicketCost { get; set; }
        public int MaxViewers { get; set; }
        public DateTime? StartTime { get; set; }

        public string HomeTeamId { get; set; } = string.Empty;
        public string AwayTeamId { get; set; } = string.Empty;
        public string TournamentId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;

        public int RoundNumber { get; set; }
        public string? GroupId { get; set; }
        public string? StageId { get; set; }
        public int BracketPosition { get; set; }

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }

        public string? WinnerId { get; set; }
        public bool IsAutomatic { get; set; }
    }
}