using Beckend.Models;


namespace Beckend.Repositories.TournamentStaffs;

public interface ITournamentStaffRepository
{
    Task<TournamentStaff?> GetActiveAsync(
        string tournamentId,
        string userId);

    Task<List<TournamentStaff>> GetByTournamentAsync(
        string tournamentId);

    Task<TournamentStaff> AssignAsync(
        TournamentStaff staff);

    Task<bool> UpdatePermissionsAsync(
        string tournamentId,
        string userId,
        Beckend.Enums.TournamentPermission permissions);

    Task<bool> RevokeAsync(
        string tournamentId,
        string userId);
    Task EnsureIndexesAsync();
}
