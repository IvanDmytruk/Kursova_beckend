using Beckend.Models;
using Beckend.Repositories.Common;

namespace Beckend.Repositories.Statistics;

public interface IStatisticRepository : IRepository<Statistic>
{
    Task<List<Statistic>> GetStatisticsByUserIdAsync(
        string userId);

    Task<List<Statistic>> GetStatisticsByTeamIdAsync(
        string teamId);

    Task<List<Statistic>> GetStatisticsByTournamentIdAsync(
        string tournamentId);

    Task<List<Statistic>> GetStatisticsBySeasonAsync(
        string season);

    Task<List<Statistic>> GetTopPlayersByPointsAsync(
        int limit);
}
