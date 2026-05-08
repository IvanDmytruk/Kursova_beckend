// TournamentService.cs
using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories;

namespace Beckend.Services
{
    public class TournamentService
    {
        private readonly TournamentRepository _tournamentRepository;
        private readonly TeamRepository _teamRepository;
        private readonly IMapper _mapper;

        public TournamentService(TournamentRepository tournamentRepository, IMapper mapper, TeamRepository teamRepository)
        {
            _tournamentRepository = tournamentRepository;
            _mapper = mapper;
            _teamRepository = teamRepository;
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
        public async Task<List<TournamentDto>> GetActiveTournamentsAsync(SportName? sport = null)
        {
            var tournaments = await _tournamentRepository.GetActiveTournamentsAsync();

            if (sport.HasValue)
            {
                tournaments = tournaments.Where(t => t.SportName == sport.Value).ToList();
            }

            var tournamentDtos = _mapper.Map<List<TournamentDto>>(tournaments);
            return tournamentDtos;
        }
        public async Task<bool> RegisterPlayerAsync(string tournamentId, string userId)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournament.StartDate < DateTime.UtcNow)
                throw new InvalidOperationException("Tournament has already started, registration closed");

            if (tournament.Format != TournamentFormat.Individual)
                throw new InvalidOperationException("This tournament is for teams, not individuals");

            if (tournament.MaxParticipants > 0 && tournament.TournamentParticipants.Count >= tournament.MaxParticipants)
                throw new InvalidOperationException($"Tournament is full. Maximum {tournament.MaxParticipants} participants allowed");

            if (tournament.TournamentParticipants.Any(p => p.Id == userId))
                throw new InvalidOperationException("User already registered for this tournament");

            var player = new Team
            {
                Id = userId,
                TeamName = $"Player_{userId.Substring(0, 6)}",
                TeamDescription = "Individual player",
                SportName = tournament.SportName
            };

            tournament.TournamentParticipants.Add(player);
            await _tournamentRepository.UpdateAsync(tournamentId, tournament);

            return true;
        }

        public async Task<bool> RegisterTeamAsync(string tournamentId, string teamId)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournament.StartDate < DateTime.UtcNow)
                throw new InvalidOperationException("Tournament has already started, registration closed");

            if (tournament.Format != TournamentFormat.Command)
                throw new InvalidOperationException("This tournament is for individuals, not teams");

            if (tournament.MaxParticipants > 0 && tournament.TournamentParticipants.Count >= tournament.MaxParticipants)
                throw new InvalidOperationException($"Tournament is full. Maximum {tournament.MaxParticipants} teams allowed");

            var team = await _teamRepository.GetByIdAsync(teamId);
            if (team == null)
                throw new KeyNotFoundException("Team not found");

            if (tournament.TournamentParticipants.Any(p => p.Id == teamId))
                throw new InvalidOperationException("Team already registered for this tournament");

            tournament.TournamentParticipants.Add(team);
            await _tournamentRepository.UpdateAsync(tournamentId, tournament);

            return true;
        }
    }
}