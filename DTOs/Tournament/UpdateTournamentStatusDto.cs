using System.ComponentModel.DataAnnotations;

namespace Beckend.DTOs
{
    public class UpdateTournamentStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}