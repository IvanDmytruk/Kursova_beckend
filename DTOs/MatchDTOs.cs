namespace Beckend.DTOs
{
    public class MatchDto
    {
        public string Id { get; set; }
        public string MatchName { get; set; }
        public double TicketCost { get; set; }
        public int MaxViewers { get; set; }
        public DateTime StartTime { get; set; }
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public string TournamentId { get; set; }
        public string Status { get; set; }
    }

    public class CreateMatchDto
    {
        public string MatchName { get; set; }
        public double TicketCost { get; set; }
        public int MaxViewers { get; set; }
        public DateTime StartTime { get; set; }
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public string TournamentId { get; set; }
    }

    public class UpdateMatchDto
    {
        public string MatchName { get; set; }
        public double TicketCost { get; set; }
        public int MaxViewers { get; set; }
        public DateTime StartTime { get; set; }
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public string TournamentId { get; set; }
        public string Status { get; set; }
    }

    public class UpdateMatchStatusDto
    {
        public string Status { get; set; }
    }
}
