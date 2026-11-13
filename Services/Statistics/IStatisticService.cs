using Beckend.DTOs;

namespace Beckend.Services.Statistics;

public interface IStatisticService
{
    Task<List<StatisticDto>> GetStatisticsByUserIdAsync(
        string userId);

    Task<List<StatisticDto>> GetStatisticsByTeamIdAsync(
        string teamId);

    Task<List<StatisticDto>> GetStatisticsByTournamentIdAsync(
        string tournamentId);

    Task<List<StatisticDto>> GetStatisticsBySeasonAsync(
        string season);

    Task<List<StatisticDto>> GetTopPlayersAsync(
        int limit = 10);

    Task<List<StatisticDto>> GetAllAsync();

    Task<StatisticDto> GetByIdAsync(string id);

    Task<StatisticDto> CreateAsync(
        CreateStatisticDto createDto);

    Task<StatisticDto> UpdateAsync(
        string id,
        UpdateStatisticDto updateDto);

    Task<bool> DeleteAsync(string id);

    Task<StatisticDto> UpdateMatchStatsAsync(
        string id,
        bool isWin,
        bool isDraw = false);
}
