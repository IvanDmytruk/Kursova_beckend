using Beckend.DTOs;
using Beckend.Models;

namespace Beckend.Services.Teams
{
    public interface ITeamService
    {
        Task<TeamDto> GetTeamByNameAsync(string name);

        Task<List<TeamDto>> SearchTeamsAsync(
            string searchTerm);

        Task<List<User>> GetPlayersByTeamIdAsync(
            string teamId);

        Task<bool> TeamNameExistsAsync(string name);

        Task<List<TeamDto>> GetAllAsync();

        Task<TeamDto?> GetByIdAsync(string id);

        Task<TeamDto> CreateAsync(CreateTeamDto dto);

        Task<TeamDto> UpdateAsync(
            string id,
            UpdateTeamDto dto);

        Task<bool> DeleteAsync(string id);

        Task<List<TeamDto>> GetTeamsPagedAsync(
            int page,
            int pageSize);
    }
}
