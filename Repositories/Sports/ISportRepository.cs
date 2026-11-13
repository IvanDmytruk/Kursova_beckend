using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Common;

namespace Beckend.Repositories.Sports;

public interface ISportRepository : IRepository<Sport>
{
    Task<Sport?> GetSportByNameAsync(SportName sportName);

    Task<List<Sport>> GetSportsByTypeAsync(TypeSport type);

    Task<List<Sport>> GetActiveSportsAsync();

    Task<List<Sport>> GetSportsByDescriptionSearchAsync(
        string searchTerm);
}
