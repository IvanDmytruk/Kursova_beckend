using Beckend.DTOs;
using Beckend.Enums;

namespace Beckend.Services.Sports;

public interface ISportService
{
    Task<SportDto> GetSportByNameAsync(
        SportName sportName);

    Task<List<SportDto>> GetSportsByTypeAsync(
        TypeSport type);

    Task<List<SportDto>> GetActiveSportsAsync();

    Task<List<SportDto>> SearchSportsByDescriptionAsync(
        string searchTerm);

    Task<bool> SportExistsAsync(
        SportName sportName);

    Task<List<SportDto>> GetAllAsync();

    Task<SportDto> GetByIdAsync(string id);

    Task<SportDto> CreateAsync(
        CreateSportDto createDto);

    Task<SportDto> UpdateAsync(
        string id,
        UpdateSportDto updateDto);

    Task<bool> DeleteAsync(string id);

    Task<SportDto> ToggleSportActiveAsync(string id);
}
