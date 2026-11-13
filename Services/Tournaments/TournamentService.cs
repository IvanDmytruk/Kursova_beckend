using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Teams;
using Beckend.Repositories.Tournaments;

namespace Beckend.Services.Tournaments
{
    public class TournamentService : ITournamentService
    {
        private readonly ITournamentRepository _tournamentRepository;
        private readonly IMapper _mapper;

        public TournamentService(ITournamentRepository tournamentRepository, IMapper mapper)
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
        public async Task<List<TournamentDto>> GetActiveTournamentsAsync(
    SportName? sport)
        {
            var tournaments = await _tournamentRepository.GetActiveTournamentsAsync();

            if (sport.HasValue)
            {
                tournaments = tournaments
                    .Where(t => t.SportName == sport.Value)
                    .ToList();
            }

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

        public async Task<TournamentDto?> GetByIdAsync(string id)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(id);

            if (tournament == null)
                return null;

            return _mapper.Map<TournamentDto>(tournament);
        }


        public async Task<TournamentDto> CreateAsync(CreateTournamentDto createDto)
        {
            if (string.IsNullOrWhiteSpace(createDto.TournamentName))
                throw new ArgumentException("Tournament name is required");

            var exists = await TournamentNameExistsAsync(createDto.TournamentName);

            if (exists)
                throw new InvalidOperationException(
                    $"Tournament with name '{createDto.TournamentName}' already exists");

            var tournament = _mapper.Map<Tournament>(createDto);

            if (tournament.EndDate.HasValue &&
                tournament.EndDate.Value <= tournament.StartDate)
            {
                throw new ArgumentException(
                    "Дата завершення повинна бути пізнішою за дату початку.");
            }

            tournament.Status = TournamentStatus.Draft;

            await _tournamentRepository.CreateAsync(tournament);

            return _mapper.Map<TournamentDto>(tournament);
        }


        public async Task<TournamentDto> UpdateAsync(string id, UpdateTournamentDto updateDto)
        {
            var existing = await _tournamentRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Tournament with id {id} not found");

            EnsureTournamentStructureCanBeChanged(existing, updateDto);

            if (existing.TournamentName != updateDto.TournamentName)
            {
                var exists = await TournamentNameExistsAsync(updateDto.TournamentName);
                if (exists)
                    throw new InvalidOperationException($"Tournament with name '{updateDto.TournamentName}' already exists");
            }

            _mapper.Map(updateDto, existing);

            if (existing.MaxParticipants < 0)
            {
                throw new ArgumentException(
                    "Maximum participants cannot be negative.");
            }

            if (existing.MaxParticipants > 0 &&
                existing.TournamentParticipants.Count >
                existing.MaxParticipants)
            {
                throw new InvalidOperationException(
                    "Maximum participants cannot be less " +
                    "than the current participant count.");
            }

            if (existing.EndDate.HasValue &&
                existing.EndDate.Value <= existing.StartDate)
            {
                throw new ArgumentException(
                    "Дата завершення повинна бути пізнішою за дату початку.");
            }

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

        private static void EnsureTournamentStructureCanBeChanged(
            Tournament tournament,
            UpdateTournamentDto dto)
        {
            if (tournament.Status == TournamentStatus.Draft ||
                tournament.Status == TournamentStatus.Registration)
            {
                return;
            }

            if (!Enum.TryParse<TournamentType>(
                    dto.TournamentType, true, out var newType) ||
                !Enum.TryParse<TournamentFormat>(
                    dto.Format, true, out var newFormat) ||
                !Enum.TryParse<SportName>(
                    dto.SportName, true, out var newSport))
            {
                throw new ArgumentException(
                    "Invalid tournament type, format or sport.");
            }

            if (tournament.TournamentType != newType ||
                tournament.Format != newFormat ||
                tournament.SportName != newSport ||
                tournament.MaxParticipants != dto.MaxParticipants)
            {
                throw new InvalidOperationException(
                    "Tournament structure cannot be changed " +
                    "after registration is closed.");
            }
        }
    }
}