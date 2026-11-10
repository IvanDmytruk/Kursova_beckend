using Beckend.Models;
namespace Beckend.Services.TournamentGenerators;

public class SwissRoundGenerationResult
{
    public List<Match> Matches { get; set; } = new();

    public SwissBye? Bye { get; set; }
}
