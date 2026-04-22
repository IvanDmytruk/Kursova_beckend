// TournamentService.cs
using AutoMapper;
using Beckend.Models;
using Beckend.Repositories;
using Beckend.DTOs;

namespace Beckend.Services
{
    public class TournamentService
    {
        private readonly TournamentRepository _tournamentRepository;
        private readonly IMapper _mapper;

        public TournamentService(TournamentRepository tournamentRepository, IMapper mapper)
        {
            _tournamentRepository = tournamentRepository;
            _mapper = mapper;
        }

        public async Task<TournamentDto> GetTournamentByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tournament name is required");

            var tournament = await _tournamentRepository.GetTournamentByNameAsync(name);

            if (tournament == null)
                throw new KeyNotFoundException($"Tournament with name '{name}' not found");

            return _mapper.Map<TournamentDto>(tournament);
        }

        public async Task<List<TournamentDto>> SearchTournamentsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<TournamentDto>();

            var tournaments = await _tournamentRepository.GetTournamentsByNameSearchAsync(searchTerm);
            return _mapper.Map<List<TournamentDto>>(tournaments);
        }

        public async Task<List<TournamentDto>> GetActiveTournamentsAsync()
        {
            var tournaments = await _tournamentRepository.GetActiveTournamentsAsync();
            return _mapper.Map<List<TournamentDto>>(tournaments);
        }

        public async Task<bool> TournamentNameExistsAsync(string name)
        {
            var tournament = await _tournamentRepository.GetTournamentByNameAsync(name);
            return tournament != null;
        }

        public async Task<List<TournamentDto>> GetAllAsync()
        {
            var tournaments = await _tournamentRepository.GetAllAsync();
            return _mapper.Map<List<TournamentDto>>(tournaments);
        }

        public async Task<TournamentDto> GetByIdAsync(string id)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(id);
            return _mapper.Map<TournamentDto>(tournament);
        }

        public async Task<TournamentDto> CreateAsync(CreateTournamentDto createDto)
        {
            var exists = await TournamentNameExistsAsync(createDto.TournamentName);
            if (exists)
                throw new InvalidOperationException($"Tournament with name '{createDto.TournamentName}' already exists");

            if (createDto.StartDate < DateTime.UtcNow)
                throw new ArgumentException("Start date cannot be in the past");

            if (createDto.EndDate.HasValue && createDto.EndDate <= createDto.StartDate)
                throw new ArgumentException("End date must be after start date");

            var tournament = _mapper.Map<Tournament>(createDto);
            await _tournamentRepository.CreateAsync(tournament);
            return _mapper.Map<TournamentDto>(tournament);
        }

        public async Task<TournamentDto> UpdateAsync(string id, UpdateTournamentDto updateDto)
        {
            var existing = await _tournamentRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Tournament with id {id} not found");

            if (existing.TournamentName != updateDto.TournamentName)
            {
                var exists = await TournamentNameExistsAsync(updateDto.TournamentName);
                if (exists)
                    throw new InvalidOperationException($"Tournament with name '{updateDto.TournamentName}' already exists");
            }

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _tournamentRepository.UpdateAsync(id, existing);
            return _mapper.Map<TournamentDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _tournamentRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _tournamentRepository.DeleteAsync(id);
            return true;
        }

        public async Task<bool> AddTeamToTournament(string tournamentId, string teamId)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            var team = new Team { Id = teamId };
            var result = tournament.AddParticipant(team);

            if (result)
            {
                await _tournamentRepository.UpdateAsync(tournamentId, tournament);
            }

            return result;
        }

        public async Task<bool> RemoveTeamFromTournament(string tournamentId, string teamId)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            var team = new Team { Id = teamId };
            var result = tournament.RemoveParticipant(team);

            if (result)
            {
                await _tournamentRepository.UpdateAsync(tournamentId, tournament);
            }
            return result;
        }
    }
}