using Beckend.Models;
using Beckend.Repositories.Common;

namespace Beckend.Repositories.Teams
{
    public interface ITeamRepository : IRepository<Beckend.Models.Team>
    {
        Task<Beckend.Models.Team?> GetTeamByNameAsync(
            string name);

        Task<List<Beckend.Models.Team>>
            GetTeamsByNameSearchAsync(string searchTerm);

        Task<List<Beckend.Models.Team>>
            GetAllTeamsPagedAsync(int page, int pageSize);
    }
}
