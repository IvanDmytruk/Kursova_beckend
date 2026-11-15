using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Common;

namespace Beckend.Repositories.Users;

public interface IUserRepository : IRepository<User>
{
    Task<List<User>> SearchPlayersAndCoachesAsync( string? searchTerm);
    Task<List<User>> GetAllPlayersAsync();
    Task<List<User>> GetAllCoachesAsync();
    Task<List<User>> GetPlayersAndCoachesByAgeRangeAsync( int minAge,int maxAge);
    Task<List<User>> GetPlayersByTeamIdAsync( string teamId);
    Task<List<User>> SearchPlayersAsync(string? searchTerm);
    Task<bool> HasTeamRoleAsync( string userId,params TeamRole[] roles);
}
