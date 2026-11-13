
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Common;
using MongoDB.Driver;

namespace Beckend.Repositories.Tournaments;

public interface ITournamentRepository : IRepository<Tournament>
{
    Task<Tournament?> GetTournamentByNameAsync(string name);

    Task<List<Tournament>> GetTournamentsByNameSearchAsync(
        string searchTerm);

    Task<List<Tournament>> GetTournamentsByTypeAsync(
        TournamentType type);

    Task<List<Tournament>> GetActiveTournamentsAsync();

    Task<bool> CompleteOlympicTournamentAsync(
        string tournamentId);

    Task<bool> CompleteOlympicTournamentAsync(
        IClientSessionHandle session,
        string tournamentId);

    Task<bool> StartOlympicTournamentAsync(
        IClientSessionHandle session,
        string tournamentId);

    Task<bool> StartTournamentAsync(
        IClientSessionHandle session,
        string tournamentId,
        TournamentType tournamentType);

    Task<bool> CompleteRoundRobinAsync(
        IClientSessionHandle session,
        string tournamentId);

    Task<bool> AdvanceSwissRoundAsync(
        IClientSessionHandle session,
        string tournamentId,
        int expectedRound);

    Task<bool> StartSwissTournamentAsync(
        IClientSessionHandle session,
        string tournamentId);

    Task<Tournament?> GetSwissTournamentAsync(
        IClientSessionHandle session,
        string tournamentId);

    Task<bool> CompleteFixedRoundsSwissAsync(
        IClientSessionHandle session,
        string tournamentId,
        int expectedRound);

    Task<bool> CompleteThresholdSwissAsync(
        IClientSessionHandle session,
        string tournamentId);
    Task<bool> CompleteCs2SwissAsync(
    IClientSessionHandle session,
    string tournamentId);
    Task<bool> StartDoubleEliminationAsync(
    IClientSessionHandle session,
    string tournamentId);

    Task<bool> AdvanceDoubleEliminationRoundAsync(
        IClientSessionHandle session,
        string tournamentId,
        int expectedRound);

    Task<bool> CompleteDoubleEliminationAsync(
        IClientSessionHandle session,
        string tournamentId,
        string championId,
        int expectedRound);
}
