using Beckend.DTOs.Standings;
using Beckend.Enums;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Tournaments;

namespace Beckend.Services.TournamentStandings
{
    public class StandingsService
    {
        private readonly ITournamentRepository _tournamentRepository;
        private readonly IMatchRepository _matchRepository;
        private readonly IEnumerable<IStandingsGenerator> _generators;
        private readonly GroupStageStandingsGenerator _groupStageStandings;

        public StandingsService(
            ITournamentRepository tournamentRepository,
            IMatchRepository matchRepository,
            IEnumerable<IStandingsGenerator> generators,
            GroupStageStandingsGenerator groupStageStandings)
        {
            _tournamentRepository = tournamentRepository;
            _matchRepository = matchRepository;
            _generators = generators;
            _groupStageStandings = groupStageStandings;
        }

        public async Task<TournamentStandingsDto> GetStandingsAsync(
            string tournamentId)
        {
            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");
            if (tournament.TournamentType == TournamentType.GroupStage)
            {
                var groupMatches = await _matchRepository
                    .GetMatchesByTournamentIdAsync(tournamentId);

                return await _groupStageStandings.GenerateAsync(
                    tournament,
                    groupMatches);
            }
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
