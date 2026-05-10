using Beckend.Enums;

namespace Beckend.DTOs
{
    public class TournamentDto
    {
        public string Id { get; set; }
        public string TournamentName { get; set; }
        public string TournamentDescription { get; set; }
        public string TournamentType { get; set; }
        public string Format { get; set; }
        public int PrizeFund { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<TeamDto> TournamentParticipants { get; set; }
        public int NumberOfParticipants { get; set; }
        public string SportName { get; set; }
        public int MaxParticipants { get; set; }
    }

    public class CreateTournamentDto
    {
        public string TournamentName { get; set; }
        public string TournamentDescription { get; set; }
        public string TournamentType { get; set; }
        public string Format { get; set; }
        public int PrizeFund { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SportName { get; set; }

        public string? CreatedBy { get; set; }
        public int MaxParticipants { get; set; }
    }

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

    public class AddTeamToTournamentDto
    {
        public string TeamId { get; set; }
    }
}