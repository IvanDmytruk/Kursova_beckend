// TeamService.cs
using AutoMapper;
using Beckend.DTOs;
using Beckend.Models;
using Beckend.Repositories;
using System.Numerics;

namespace Beckend.Services
{
    public class TeamService
    {
        private readonly TeamRepository _teamRepository;
        private readonly UserRepository _userRepository;
        private readonly IMapper _mapper;

        public TeamService(TeamRepository teamRepository, IMapper mapper, UserRepository userRepository)
        {
            _teamRepository = teamRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<TeamDto> GetTeamByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Team name is required");

            var team = await _teamRepository.GetTeamByNameAsync(name);

            if (team == null)
                throw new KeyNotFoundException($"Team with name '{name}' not found");

            return _mapper.Map<TeamDto>(team);
        }
        public async Task<List<TeamDto>> SearchTeamsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<TeamDto>();

            var teams = await _teamRepository.GetTeamsByNameSearchAsync(searchTerm);
            return _mapper.Map<List<TeamDto>>(teams);
        }
        public async Task<List<User>> GetPlayersByTeamIdAsync(string teamId)
        {
            var team = await _teamRepository.GetByIdAsync(teamId);
            if (team == null)
            {
                throw new KeyNotFoundException($"Team with id {teamId} not found");
            }

            return await _userRepository.GetPlayersByTeamIdAsync(teamId);
        }

        public async Task<bool> TeamNameExistsAsync(string name)
        {
            var team = await _teamRepository.GetTeamByNameAsync(name);
            return team != null;
        }

        public async Task<List<TeamDto>> GetAllAsync()
        {
            var teams = await _teamRepository.GetAllAsync();
            return _mapper.Map<List<TeamDto>>(teams);
        }

        public async Task<TeamDto> GetByIdAsync(string id)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            return _mapper.Map<TeamDto>(team);
        }

        public async Task<TeamDto> CreateAsync(CreateTeamDto createDto)
        {
            var exists = await TeamNameExistsAsync(createDto.TeamName);
            if (exists)
                throw new InvalidOperationException($"Team with name '{createDto.TeamName}' already exists");

            if (string.IsNullOrWhiteSpace(createDto.TeamName))
                throw new ArgumentException("Team name is required");

            var team = _mapper.Map<Team>(createDto);
            await _teamRepository.CreateAsync(team);
            return _mapper.Map<TeamDto>(team);
        }

        public async Task<TeamDto> UpdateAsync(string id, UpdateTeamDto updateDto)
        {
            var existing = await _teamRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Team with id {id} not found");

            if (existing.TeamName != updateDto.TeamName)
            {
                var exists = await TeamNameExistsAsync(updateDto.TeamName);
                if (exists)
                    throw new InvalidOperationException($"Team with name '{updateDto.TeamName}' already exists");
            }

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _teamRepository.UpdateAsync(id, existing);
            return _mapper.Map<TeamDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _teamRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _teamRepository.DeleteAsync(id);
            return true;
        }

        public async Task<List<TeamDto>> GetTeamsPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var teams = await _teamRepository.GetAllTeamsPagedAsync(page, pageSize);
            return _mapper.Map<List<TeamDto>>(teams);
        }
    }
}