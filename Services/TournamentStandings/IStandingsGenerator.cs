using Beckend.DTOs.Standings;
using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentStandings
{
    public interface IStandingsGenerator
    {
        TournamentType SupportedType { get; }

        TournamentStandingsDto Generate(
            Tournament tournament,
            List<Match> matches);
    }
}
