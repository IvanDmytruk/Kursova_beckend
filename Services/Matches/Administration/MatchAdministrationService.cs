using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Matches.Results;

namespace Beckend.Services.Matches.Administration;

public class MatchAdministrationService
    : IMatchAdministrationService
{
    private readonly IMatchRepository _matches;
    private readonly ITournamentRepository _tournaments;
    private readonly IMatchResultService _results;

    public MatchAdministrationService(
        IMatchRepository matches,
        ITournamentRepository tournaments,
        IMatchResultService results)
    {
        _matches = matches;
        _tournaments = tournaments;
        _results = results;
    }

    public async Task<MatchDto> SetTechnicalResultAsync(
        string matchId,
        AdminMatchResultDto dto,
        string administratorId)
    {
        if (string.IsNullOrWhiteSpace(administratorId))
            throw new ArgumentException(
                "Administrator ID is required.");

        if (dto.ResultType == MatchResultType.Normal ||
            !Enum.IsDefined(dto.ResultType))
        {
            throw new ArgumentException(
                "Invalid administrative result type.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason) ||
            dto.Reason.Trim().Length < 5)
        {
            throw new ArgumentException(
                "Reason must contain at least 5 characters.");
        }

        var match = await _matches.GetByIdAsync(matchId)
            ?? throw new KeyNotFoundException("Match not found");

        var tournament = await _tournaments
            .GetByIdAsync(match.TournamentId)
            ?? throw new KeyNotFoundException("Tournament not found");

        if (tournament.Status != TournamentStatus.InProgress)
            throw new InvalidOperationException(
                "Tournament must be InProgress.");

        if (match.Status == MatchStatus.Completed ||
            match.Status == MatchStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Match is already finalized.");
        }

        if (dto.WinnerTeamId != match.HomeTeamId &&
            dto.WinnerTeamId != match.AwayTeamId)
        {
            throw new ArgumentException(
                "Winner must be a match participant.");
        }

        if (dto.HomeScore == dto.AwayScore)
            throw new ArgumentException(
                "Technical result cannot be a draw.");

        string scoreWinner = dto.HomeScore > dto.AwayScore
            ? match.HomeTeamId
            : match.AwayTeamId;

        if (scoreWinner != dto.WinnerTeamId)
            throw new ArgumentException(
                "WinnerTeamId does not match the score.");

        return await _results.SetAdministrativeResultAsync(
            matchId,
            dto,
            administratorId);
    }
}
