// MatchService.cs
using AutoMapper;
using Beckend.Models;
using Beckend.Repositories;
using Beckend.DTOs;

namespace Beckend.Services
{
    public class MatchService
    {
        private readonly MatchRepository _matchRepository;
        private readonly IMapper _mapper;

        public MatchService(MatchRepository matchRepository, IMapper mapper)
        {
            _matchRepository = matchRepository;
            _mapper = mapper;
        }

        public async Task<MatchDto> GetMatchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Match name is required");

            var match = await _matchRepository.GetMatchByNameAsync(name);

            if (match == null)
                throw new KeyNotFoundException($"Match with name '{name}' not found");

            return _mapper.Map<MatchDto>(match);
        }

        public async Task<List<MatchDto>> GetMatchesByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID is required");

            var matches = await _matchRepository.GetMatchesByTournamentIdAsync(tournamentId);
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<List<MatchDto>> GetMatchesByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID is required");

            var matches = await _matchRepository.GetMatchesByTeamIdAsync(teamId);
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<List<MatchDto>> GetUpcomingMatchesAsync()
        {
            var matches = await _matchRepository.GetUpcomingMatchesAsync();
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<List<MatchDto>> GetMatchesByDateRangeAsync(DateTime start, DateTime end)
        {
            if (start > end)
                throw new ArgumentException("Start date must be before end date");

            var matches = await _matchRepository.GetMatchesByDateRangeAsync(start, end);
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<bool> MatchExistsAsync(string name)
        {
            var match = await _matchRepository.GetMatchByNameAsync(name);
            return match != null;
        }

        public async Task<List<MatchDto>> GetAllAsync()
        {
            var matches = await _matchRepository.GetAllAsync();
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<MatchDto> GetByIdAsync(string id)
        {
            var match = await _matchRepository.GetByIdAsync(id);
            return _mapper.Map<MatchDto>(match);
        }

        public async Task<MatchDto> CreateAsync(CreateMatchDto createDto)
        {
            var exists = await MatchExistsAsync(createDto.MatchName);
            if (exists)
                throw new InvalidOperationException($"Match with name '{createDto.MatchName}' already exists");

            if (string.IsNullOrWhiteSpace(createDto.MatchName))
                throw new ArgumentException("Match name is required");

            if (createDto.StartTime < DateTime.UtcNow)
                throw new ArgumentException("Start time cannot be in the past");

            if (createDto.TicketCost < 0)
                throw new ArgumentException("Ticket cost cannot be negative");

            if (createDto.MaxViewers <= 0)
                throw new ArgumentException("Max viewers must be greater than 0");

            if (string.IsNullOrWhiteSpace(createDto.HomeTeamId))
                throw new ArgumentException("Home team is required");

            if (string.IsNullOrWhiteSpace(createDto.AwayTeamId))
                throw new ArgumentException("Away team is required");

            if (createDto.HomeTeamId == createDto.AwayTeamId)
                throw new ArgumentException("Home team and away team cannot be the same");

            var match = _mapper.Map<Match>(createDto);
            await _matchRepository.CreateAsync(match);
            return _mapper.Map<MatchDto>(match);
        }

        public async Task<MatchDto> UpdateAsync(string id, UpdateMatchDto updateDto)
        {
            var existing = await _matchRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Match with id {id} not found");

            if (existing.MatchName != updateDto.MatchName)
            {
                var exists = await MatchExistsAsync(updateDto.MatchName);
                if (exists)
                    throw new InvalidOperationException($"Match with name '{updateDto.MatchName}' already exists");
            }

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _matchRepository.UpdateAsync(id, existing);
            return _mapper.Map<MatchDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _matchRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _matchRepository.DeleteAsync(id);
            return true;
        }

        public async Task<List<MatchDto>> GetMatchesStartingSoonAsync(int hours = 24)
        {
            var now = DateTime.UtcNow;
            var soon = now.AddHours(hours);

            var matches = await _matchRepository.GetMatchesByDateRangeAsync(now, soon);
            return _mapper.Map<List<MatchDto>>(matches);
        }
    }
}