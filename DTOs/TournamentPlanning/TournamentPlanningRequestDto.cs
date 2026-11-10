using System.ComponentModel.DataAnnotations;
using Beckend.Enums;

namespace Beckend.DTOs.TournamentPlanning;

public class TournamentPlanningRequestDto
{
    [Range(2, 256)]
    public int ParticipantCount { get; set; }

    [Range(2, 256)]
    public int PlayoffSize { get; set; }

    public TournamentType PreferredType { get; set; }
        = TournamentType.GroupStage;

    [Range(1, 10)]
    public int MatchesPerPair { get; set; } = 1;

    public int? MaxMatches { get; set; }
    public int? MaxRounds { get; set; }
}
