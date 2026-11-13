using Beckend.DTOs;
using Beckend.Enums;

namespace Beckend.Services.Tournaments;

public interface ITournamentService
{
    Task<TournamentDto> GetTournamentByNameAsync(string name);
    Task<List<TournamentDto>> SearchTournamentsAsync(string searchTerm);
    Task<List<TournamentDto>> GetActiveTournamentsAsync();
    Task<List<TournamentDto>> GetActiveTournamentsAsync(SportName? sport);
    Task<bool> TournamentNameExistsAsync(string name);

    Task<List<TournamentDto>> GetAllAsync();
    Task<TournamentDto?> GetByIdAsync(string id);
    Task<TournamentDto> CreateAsync(CreateTournamentDto createDto);
    Task<TournamentDto> UpdateAsync(string id, UpdateTournamentDto updateDto);
    Task<bool> DeleteAsync(string id);
}
