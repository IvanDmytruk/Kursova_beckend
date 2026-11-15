using Beckend.Enums;
using StaffModel = Beckend.Models.TournamentStaff;

namespace Beckend.Services.TournamentStaff;

public interface ITournamentStaffService
{
    Task<StaffModel> AssignManagerAsync(
        string tournamentId,
        string userId,
        TournamentPermission permissions,
        string organizerId);

    Task<bool> UpdateManagerPermissionsAsync(
        string tournamentId,
        string managerId,
        TournamentPermission permissions,
        string organizerId);

    Task<bool> RevokeManagerAsync(
        string tournamentId,
        string managerId,
        string organizerId);

    Task<List<StaffModel>> GetManagersAsync(
        string tournamentId,
        string requesterId);
}
