using Beckend.DTOs.Olympic;

namespace Beckend.Services.Olympic.Interfaces;

public interface IOlympicBracketService
{
    Task<OlympicBracketResponseDto> GenerateFirstRoundAsync(
        string tournamentId);

    Task<OlympicBracketResponseDto> GenerateNextRoundAsync(
        string tournamentId);
}
