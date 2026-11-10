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

        public async Task<bool> AddTeamToTournament(string tournamentId, string teamId)
        {

            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            EnsureParticipantsCanBeChanged(tournament);

            var team = await _teamRepository.GetByIdAsync(teamId);

            if (team == null)
                throw new KeyNotFoundException("Team not found");
            var result = tournament.AddParticipant(team);

            if (result)
            {
                await _tournamentRepository.UpdateAsync(tournamentId, tournament);
            }

            return result;
        }

        public async Task<bool> RemoveTeamFromTournament(string tournamentId, string teamId)
        {

            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            EnsureParticipantsCanBeChanged(tournament);

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

            if (tournament.Status != TournamentStatus.Registration)
            {
                throw new InvalidOperationException(
                    "Registration is not open for this tournament");
            }

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

            if (tournament.Status != TournamentStatus.Registration)
                throw new InvalidOperationException(
                    "Registration is not open for this tournament");

            if (tournament.Format != TournamentFormat.Command)
                throw new InvalidOperationException(
                    "This tournament is for individuals, not teams");

            if (tournament.MaxParticipants < 0)
                throw new InvalidOperationException(
                    "Tournament has an invalid participant limit");

            if (tournament.MaxParticipants > 0 &&
                tournament.TournamentParticipants.Count >= tournament.MaxParticipants)
                throw new InvalidOperationException(
                    "Tournament is full");

            var team = await _teamRepository.GetByIdAsync(teamId);

            if (team == null)
                throw new KeyNotFoundException("Team not found");

            if (team.SportName != tournament.SportName)
                throw new InvalidOperationException(
                    "Team sport does not match tournament sport");

            if (tournament.TournamentParticipants.Any(p => p.Id == teamId))
                throw new InvalidOperationException(
                    "Team already registered for this tournament");

            tournament.TournamentParticipants.Add(team);

            await _tournamentRepository.UpdateAsync(tournamentId, tournament);

            return true;
        }

        public async Task<TournamentDto> ChangeStatusAsync(string tournamentId,TournamentStatus newStatus)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");


            if (tournament.Status == TournamentStatus.Ready &&
                newStatus == TournamentStatus.InProgress &&
                (tournament.TournamentType == TournamentType.RoundRobin ||
                 tournament.TournamentType == TournamentType.SwissSystem))
            {
                throw new InvalidOperationException(
                    "Tournament must be started by generating its matches.");
            }

            if (tournament.TournamentType == TournamentType.OlympicSystem &&
                (newStatus == TournamentStatus.InProgress ||
                 newStatus == TournamentStatus.Completed))
            {
                throw new InvalidOperationException(
                    "Olympic tournament status is managed automatically " +
                    "by OlympicBracketService.");
            }

            var allowedTransitions =
                new Dictionary<TournamentStatus, TournamentStatus[]>
                {
                    [TournamentStatus.Draft] =
                        new[] { TournamentStatus.Registration, TournamentStatus.Cancelled },

                    [TournamentStatus.Registration] =
                        new[] { TournamentStatus.Ready, TournamentStatus.Cancelled },

                    [TournamentStatus.Ready] =
                        new[] { TournamentStatus.InProgress, TournamentStatus.Cancelled },

                    [TournamentStatus.InProgress] =
                        new[] { TournamentStatus.Completed, TournamentStatus.Cancelled },

                    [TournamentStatus.Completed] =
                        Array.Empty<TournamentStatus>(),

                    [TournamentStatus.Cancelled] =
                        Array.Empty<TournamentStatus>()
                };

            if (!allowedTransitions.TryGetValue(
                    tournament.Status, out var allowed) ||
                !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException(
                    $"Cannot change tournament status from {tournament.Status} to {newStatus}");
            }

            if (newStatus == TournamentStatus.Ready &&
                tournament.TournamentParticipants.Count < 2)
            {
                throw new InvalidOperationException(
                    "At least 2 participants are required");
            }

            tournament.Status = newStatus;

            await _tournamentRepository.UpdateAsync(tournamentId, tournament);

            return _mapper.Map<TournamentDto>(tournament);
        }

        private static void EnsureParticipantsCanBeChanged(
            Tournament tournament)
        {
            if (tournament.Status != TournamentStatus.Draft &&
                tournament.Status != TournamentStatus.Registration)
            {
                throw new InvalidOperationException(
                    "Participants can only be changed during " +
                    "Draft or Registration.");
            }
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