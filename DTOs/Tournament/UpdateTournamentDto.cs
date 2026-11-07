namespace Beckend.DTOs
{
    public class UpdateTournamentDto
    {
        public string TournamentName { get; set; }
        public string TournamentDescription { get; set; }
        public string TournamentType { get; set; }
        public string Format { get; set; }
        public int PrizeFund { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SportName { get; set; }
        public int MaxParticipants { get; set; }
    }
}
