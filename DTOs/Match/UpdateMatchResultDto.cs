using System.ComponentModel.DataAnnotations;

namespace Beckend.DTOs
{
    public class UpdateMatchResultDto
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int? HomeScore { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int? AwayScore { get; set; }
    }
}