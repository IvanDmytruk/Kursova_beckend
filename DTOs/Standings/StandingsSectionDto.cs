
namespace Beckend.DTOs.Standings
{
    public class StandingsSectionDto
    {
        public string SectionId { get; set; } = string.Empty;

        public string SectionName { get; set; } = string.Empty;

        public List<StandingsEntryDto> Entries { get; set; } = new();
    }
}
