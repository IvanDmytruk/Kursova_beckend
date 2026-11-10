
using Beckend.DTOs.Standings;
using Beckend.Repositories;

namespace Beckend.Services.TournamentStandings
{
    public class StandingsService
    {
        private readonly TournamentRepository _tournamentRepository;
        private readonly MatchRepository _matchRepository;
        private readonly IEnumerable<IStandingsGenerator> _generators;

        public StandingsService(
            TournamentRepository tournamentRepository,
            MatchRepository matchRepository,
            IEnumerable<IStandingsGenerator> generators)
        {
            _tournamentRepository = tournamentRepository;
            _matchRepository = matchRepository;
            _generators = generators;
        }

        public async Task<TournamentStandingsDto> GetStandingsAsync(
            string tournamentId)
        {
            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            var generator = _generators.FirstOrDefault(
                g => g.SupportedType == tournament.TournamentType);

            if (generator == null)
                throw new NotSupportedException(
                    $"Standings for {tournament.TournamentType} are not implemented");

            var matches = await _matchRepository
                .GetMatchesByTournamentIdAsync(tournamentId);

            return generator.Generate(tournament, matches);
        }
    }
}
