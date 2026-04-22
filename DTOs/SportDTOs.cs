namespace Beckend.DTOs
{
    public class SportDto
    {
        public string Id { get; set; }
        public string SportName { get; set; }
        public string Type { get; set; }
        public string SportDescription { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSportDto
    {
        public string SportName { get; set; }
        public string Type { get; set; }
        public string SportDescription { get; set; }
    }

    public class UpdateSportDto
    {
        public string SportName { get; set; }
        public string Type { get; set; }
        public string SportDescription { get; set; }
        public bool IsActive { get; set; }
    }
}
