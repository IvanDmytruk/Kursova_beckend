namespace Beckend.DTOs
{
    public class StatisticDto
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string TeamId { get; set; }
        public string TournamentId { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int Points { get; set; }
        public int MatchesPlayed { get; set; }
        public string Season { get; set; }
    }
}
