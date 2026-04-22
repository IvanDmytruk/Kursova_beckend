using Beckend.Models;
using Beckend.Repositories;

namespace Beckend.Services
{
    public class TournamentService
    {
        private readonly TournamentRepository _tournamentRepository;

        public TournamentService(TournamentRepository tournamentRepository)
        {
            _tournamentRepository = tournamentRepository;
        }

        // Отримати турнір за назвою
        public async Task<Tournament?> GetTournamentByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tournament name is required");

            var tournament = await _tournamentRepository.GetTournamentByNameAsync(name);

            if (tournament == null)
                throw new KeyNotFoundException($"Tournament with name '{name}' not found");

            return tournament;
        }

        // Пошук турнірів за назвою (частковий збіг)
        public async Task<List<Tournament>> SearchTournamentsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Tournament>();

            return await _tournamentRepository.GetTournamentsByNameSearchAsync(searchTerm);
        }

        //Отримати активні турніри
        public async Task<List<Tournament>> GetActiveTournamentsAsync()
        {
            return await _tournamentRepository.GetActiveTournamentsAsync();
        }

        // Перевірити чи існує турнір з такою назвою
        public async Task<bool> TournamentNameExistsAsync(string name)
        {
            var tournament = await _tournamentRepository.GetTournamentByNameAsync(name);
            return tournament != null;
        }

        // Існуючі методи CRUD
        public async Task<List<Tournament>> GetAllAsync() =>
            await _tournamentRepository.GetAllAsync();

        public async Task<Tournament?> GetByIdAsync(string id) =>
            await _tournamentRepository.GetByIdAsync(id);

        public async Task<Tournament> CreateAsync(Tournament tournament)
        {
            // Перевірка унікальності назви
            var exists = await TournamentNameExistsAsync(tournament.TournamentName);
            if (exists)
                throw new InvalidOperationException($"Tournament with name '{tournament.TournamentName}' already exists");

            // Валідація дат
            if (tournament.StartDate < DateTime.UtcNow)
                throw new ArgumentException("Start date cannot be in the past");

            if (tournament.EndDate.HasValue && tournament.EndDate <= tournament.StartDate)
                throw new ArgumentException("End date must be after start date");

            await _tournamentRepository.CreateAsync(tournament);
            return tournament;
        }

        public async Task<Tournament> UpdateAsync(string id, Tournament tournament)
        {
            var existing = await _tournamentRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Tournament with id {id} not found");

            // Перевірка унікальності назви (якщо назва змінилась)
            if (existing.TournamentName != tournament.TournamentName)
            {
                var exists = await TournamentNameExistsAsync(tournament.TournamentName);
                if (exists)
                    throw new InvalidOperationException($"Tournament with name '{tournament.TournamentName}' already exists");
            }

            tournament.Id = id;
            await _tournamentRepository.UpdateAsync(id, tournament);
            return tournament;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _tournamentRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _tournamentRepository.DeleteAsync(id);
            return true;
        }

        // Робота з учасниками
        public async Task<bool> AddTeamToTournament(string tournamentId, Team team)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            var result = tournament.AddParticipant(team);

            if (result)
            {
                await _tournamentRepository.UpdateAsync(tournamentId, tournament);
            }

            return result;
        }
        public async Task<bool> RemoveTeamFromTournament(string tournamentId, Team team)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            var result = tournament.RemoveParticipant(team); 

            if (result)
            {
                await _tournamentRepository.UpdateAsync(tournamentId, tournament);
            }
            return result;
        }
    }
}