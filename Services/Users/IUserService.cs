using Beckend.DTOs;
using Beckend.Models;

namespace Beckend.Services.Users;

public interface IUserService
{
    Task<List<UserDto>> SearchPlayersAndCoachesAsync(
        string? searchTerm);

    Task<List<UserDto>> GetAllPlayersAsync();

    Task<List<UserDto>> GetAllCoachesAsync();

    Task<List<UserDto>> GetPlayersAndCoachesByAgeRangeAsync(
        int minAge,
        int maxAge);

    bool IsPlayerOrCoach(User user);

    Task<UserDto> GetPlayerOrCoachByIdAsync(string id);

    Task<List<UserDto>> GetAllAsync();

    Task<UserDto> GetByIdAsync(string id);

    Task<UserDto> CreateAsync(CreateUserDto dto);

    Task<UserDto> UpdateAsync(
        string id,
        UpdateUserDto dto);

    Task<bool> DeleteAsync(string id);
}
