// UserService.cs
using AutoMapper;
using Beckend.Repositories;
using Beckend.Models;
using Beckend.Enums;
using Beckend.DTOs;

namespace Beckend.Services
{
    public class UserService
    {
        private readonly UserRepository _userRepository;
        private readonly IMapper _mapper;

        public UserService(UserRepository userRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<List<UserDto>> SearchPlayersAndCoachesAsync(string? name = null, string? surname = null)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(surname))
                throw new ArgumentException("At least name or surname must be provided");

            var users = await _userRepository.SearchPlayersAndCoachesAsync(name, surname);

            if (users.Count == 0)
                throw new KeyNotFoundException("No players or coaches found");

            return _mapper.Map<List<UserDto>>(users);
        }

        public async Task<List<UserDto>> GetAllPlayersAsync()
        {
            var players = await _userRepository.GetAllPlayersAsync();

            if (players == null || players.Count == 0)
                throw new KeyNotFoundException("No players found");

            return _mapper.Map<List<UserDto>>(players);
        }

        public async Task<List<UserDto>> GetAllCoachesAsync()
        {
            var coaches = await _userRepository.GetAllCoachesAsync();

            if (coaches == null || coaches.Count == 0)
                throw new KeyNotFoundException("No coaches found");

            return _mapper.Map<List<UserDto>>(coaches);
        }

        public async Task<List<UserDto>> GetPlayersAndCoachesByAgeRangeAsync(int minAge, int maxAge)
        {
            if (minAge < 0 || maxAge < minAge)
                throw new ArgumentException("Invalid age range");

            var users = await _userRepository.GetPlayersAndCoachesByAgeRangeAsync(minAge, maxAge);

            if (users == null || users.Count == 0)
                throw new KeyNotFoundException($"No players or coaches found in age range {minAge}-{maxAge}");

            return _mapper.Map<List<UserDto>>(users);
        }

        public bool IsPlayerOrCoach(User user)
        {
            return user != null && (user.Role == UserRole.Player || user.Role == UserRole.Coach);
        }

        public async Task<UserDto> GetPlayerOrCoachByIdAsync(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                throw new KeyNotFoundException($"User with id {id} not found");

            if (!IsPlayerOrCoach(user))
                throw new UnauthorizedAccessException($"User with id {id} is not a player or coach");

            return _mapper.Map<UserDto>(user);
        }

        public async Task<List<UserDto>> GetAllAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return _mapper.Map<List<UserDto>>(users);
        }

        public async Task<UserDto> GetByIdAsync(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> CreateAsync(CreateUserDto createDto)
        {
            if (string.IsNullOrWhiteSpace(createDto.Name))
                throw new ArgumentException("User name is required");

            if (string.IsNullOrWhiteSpace(createDto.Surname))
                throw new ArgumentException("User surname is required");

            if (createDto.Age < 0 || createDto.Age > 120)
                throw new ArgumentException("Invalid age");

            var user = _mapper.Map<User>(createDto);
            await _userRepository.CreateAsync(user);
            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> UpdateAsync(string id, UpdateUserDto updateDto)
        {
            var existing = await _userRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"User with id {id} not found");

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _userRepository.UpdateAsync(id, existing);
            return _mapper.Map<UserDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _userRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _userRepository.DeleteAsync(id);
            return true;
        }
    }
}