using Beckend.DTOs;
using Beckend.Enums;

namespace Beckend.Services.Matches;

public interface IMatchService
{
    Task<MatchDto> GetMatchByNameAsync(string name);

    Task<List<MatchDto>> GetMatchesByTournamentIdAsync(
        string tournamentId);

    Task<List<MatchDto>> GetMatchesByTeamIdAsync(
        string teamId);

    Task<List<MatchDto>> GetUpcomingMatchesAsync(
        SportName? sport = null);

    Task<List<MatchDto>> GetMatchesStartingSoonAsync(int hours = 24);

    Task<List<MatchDto>> GetMatchesByDateRangeAsync( DateTime start,DateTime end);

    Task<bool> MatchExistsAsync(string name);

    Task<List<MatchDto>> GetAllAsync();

    Task<MatchDto?> GetByIdAsync(string id);

    Task<MatchDto> CreateAsync(CreateMatchDto createDto);

    Task<MatchDto> UpdateAsync(string id,UpdateMatchDto updateDto);

    Task<bool> DeleteAsync(string id);
}