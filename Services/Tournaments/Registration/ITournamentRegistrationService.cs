namespace Beckend.Services.Tournaments.Registration;

public interface ITournamentRegistrationService
{
    Task<bool> AddTeamToTournament(
        string tournamentId,
        string teamId);

    Task<bool> RemoveTeamFromTournament(
        string tournamentId,
        string teamId);

    Task<bool> RegisterPlayerAsync(
        string tournamentId,
        string userId);

    Task<bool> RegisterTeamAsync(
        string tournamentId,
        string teamId);
}
