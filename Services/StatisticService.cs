// StatisticService.cs
using AutoMapper;
using Beckend.Models;
using Beckend.Repositories;
using Beckend.DTOs;

namespace Beckend.Services
{
    public class StatisticService
    {
        private readonly StatisticRepository _statisticRepository;
        private readonly IMapper _mapper;

        public StatisticService(StatisticRepository statisticRepository, IMapper mapper)
        {
            _statisticRepository = statisticRepository;
            _mapper = mapper;
        }

        public async Task<List<StatisticDto>> GetStatisticsByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required");

            var statistics = await _statisticRepository.GetStatisticsByUserIdAsync(userId);
            return _mapper.Map<List<StatisticDto>>(statistics);
        }

        public async Task<List<StatisticDto>> GetStatisticsByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID is required");

            var statistics = await _statisticRepository.GetStatisticsByTeamIdAsync(teamId);
            return _mapper.Map<List<StatisticDto>>(statistics);
        }

        public async Task<List<StatisticDto>> GetStatisticsByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID is required");

            var statistics = await _statisticRepository.GetStatisticsByTournamentIdAsync(tournamentId);
            return _mapper.Map<List<StatisticDto>>(statistics);
        }

        public async Task<List<StatisticDto>> GetStatisticsBySeasonAsync(string season)
        {
            if (string.IsNullOrWhiteSpace(season))
                return new List<StatisticDto>();

            var statistics = await _statisticRepository.GetStatisticsBySeasonAsync(season);
            return _mapper.Map<List<StatisticDto>>(statistics);
        }

        public async Task<List<StatisticDto>> GetTopPlayersAsync(int limit = 10)
        {
            if (limit < 1) limit = 10;

            var statistics = await _statisticRepository.GetTopPlayersByPointsAsync(limit);
            return _mapper.Map<List<StatisticDto>>(statistics);
        }

        public async Task<List<StatisticDto>> GetAllAsync()
        {
            var statistics = await _statisticRepository.GetAllAsync();
            return _mapper.Map<List<StatisticDto>>(statistics);
        }

        public async Task<StatisticDto> GetByIdAsync(string id)
        {
            var statistic = await _statisticRepository.GetByIdAsync(id);
            return _mapper.Map<StatisticDto>(statistic);
        }

        public async Task<StatisticDto> CreateAsync(CreateStatisticDto createDto)
        {
            if (string.IsNullOrWhiteSpace(createDto.UserId))
                throw new ArgumentException("User ID is required");

            if (string.IsNullOrWhiteSpace(createDto.TeamId))
                throw new ArgumentException("Team ID is required");

            if (string.IsNullOrWhiteSpace(createDto.TournamentId))
                throw new ArgumentException("Tournament ID is required");

            var statistic = _mapper.Map<Statistic>(createDto);
            await _statisticRepository.CreateAsync(statistic);
            return _mapper.Map<StatisticDto>(statistic);
        }

        public async Task<StatisticDto> UpdateAsync(string id, UpdateStatisticDto updateDto)
        {
            var existing = await _statisticRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Statistic with id {id} not found");

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _statisticRepository.UpdateAsync(id, existing);
            return _mapper.Map<StatisticDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _statisticRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _statisticRepository.DeleteAsync(id);
            return true;
        }

        public async Task<StatisticDto> UpdateMatchStatsAsync(string id, bool isWin, bool isDraw = false)
        {
            var statistic = await _statisticRepository.GetByIdAsync(id);
            if (statistic == null)
                throw new KeyNotFoundException($"Statistic with id {id} not found");

            statistic.MatchesPlayed++;

            if (isWin)
            {
                statistic.Wins++;
                statistic.Points += 3;
            }
            else if (isDraw)
            {
                statistic.Draws++;
                statistic.Points += 1;
            }
            else
            {
                statistic.Losses++;
            }

            await _statisticRepository.UpdateAsync(id, statistic);
            return _mapper.Map<StatisticDto>(statistic);
        }
    }
}