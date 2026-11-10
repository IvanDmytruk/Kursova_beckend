
namespace Beckend.DTOs.Standings
{
    public class TournamentStandingsDto
    {
        public string TournamentId { get; set; } = string.Empty;

        public string TournamentType { get; set; } = string.Empty;

        public string ViewType { get; set; } = string.Empty;

        public List<StandingsSectionDto> Sections { get; set; } = new();
    }
}
