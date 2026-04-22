namespace Beckend.DTOs
{
    public class TeamDto
    {
        public string Id { get; set; }
        public string TeamName { get; set; }
        public string TeamDescription { get; set; }
    }

    public class CreateTeamDto
    {
        public string TeamName { get; set; }
        public string TeamDescription { get; set; }
    }

    public class UpdateTeamDto
    {
        public string TeamName { get; set; }
        public string TeamDescription { get; set; }
    }
}