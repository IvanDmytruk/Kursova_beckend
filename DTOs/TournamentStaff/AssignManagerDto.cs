using System.ComponentModel.DataAnnotations;
using Beckend.Enums;

namespace Beckend.DTOs.TournamentStaff;

public class AssignManagerDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public TournamentPermission Permissions { get; set; } =
        TournamentPermission.ManageMatches |
        TournamentPermission.ApproveResults |
        TournamentPermission.ViewStaff;
}
