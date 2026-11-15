namespace Beckend.Enums;

[Flags]
public enum TournamentPermission
{
    None = 0,
    ManageSchedule = 1 << 0,
    ManageMatches = 1 << 1,
    ApproveResults = 1 << 2,
    SetTechnicalResults = 1 << 3,
    DisqualifyTeams = 1 << 4,
    ManageParticipants = 1 << 5,
    ManagePlayerSanctions = 1 << 6,
    ViewAuditLog = 1 << 7,
    ViewStaff = 1 << 8,

    All = ManageSchedule
        | ManageMatches
        | ApproveResults
        | SetTechnicalResults
        | DisqualifyTeams
        | ManageParticipants
        | ManagePlayerSanctions
        | ViewAuditLog
        | ViewStaff
}
