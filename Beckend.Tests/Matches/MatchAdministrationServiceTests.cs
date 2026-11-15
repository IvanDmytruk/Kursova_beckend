using Match = Beckend.Models.Match;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Matches.Administration;
using Beckend.Services.Matches.Results;
using Moq;
using Xunit;

namespace Beckend.Tests.Matches;

public class MatchAdministrationServiceTests
{
    private const string MatchId = "507f1f77bcf86cd799439011";
    private const string TournamentId = "507f1f77bcf86cd799439012";
    private const string HomeTeamId = "507f1f77bcf86cd799439013";
    private const string AwayTeamId = "507f1f77bcf86cd799439014";
    private const string AdministratorId = "507f1f77bcf86cd799439015";

    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<IMatchResultService> _results = new();

    private readonly MatchAdministrationService _service;

    public MatchAdministrationServiceTests()
    {
        _service = new MatchAdministrationService(
            _matches.Object,
            _tournaments.Object,
            _results.Object);

        ConfigureValidMatch();
        ConfigureValidTournament();

        _results
            .Setup(r => r.SetAdministrativeResultAsync(
                It.IsAny<string>(),
                It.IsAny<AdminMatchResultDto>(),
                It.IsAny<string>()))
            .ReturnsAsync(new MatchDto());
    }

    private void ConfigureValidMatch(
        MatchStatus status = MatchStatus.Planing)
    {
        _matches
            .Setup(r => r.GetByIdAsync(MatchId))
            .ReturnsAsync(new Match
            {
                Id = MatchId,
                TournamentId = TournamentId,
                HomeTeamId = HomeTeamId,
                AwayTeamId = AwayTeamId,
                Status = status,
                MatchName = "Test Match"
            });
    }

    private void ConfigureValidTournament(
        TournamentStatus status = TournamentStatus.InProgress)
    {
        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync(new Tournament
            {
                Id = TournamentId,
                Status = status,
                CreatedBy = AdministratorId
            });
    }

    private static AdminMatchResultDto ValidDto(
        MatchResultType type = MatchResultType.TechnicalWin)
    {
        return new AdminMatchResultDto
        {
            ResultType = type,
            WinnerTeamId = HomeTeamId,
            Reason = "Opponent failed to attend the match",
            HomeScore = 3,
            AwayScore = 0
        };
    }

    private void VerifyResultCalledOnce()
    {
        _results.Verify(
            r => r.SetAdministrativeResultAsync(
                MatchId,
                It.IsAny<AdminMatchResultDto>(),
                AdministratorId),
            Times.Once);
    }

    private void VerifyResultNeverCalled()
    {
        _results.Verify(
            r => r.SetAdministrativeResultAsync(
                It.IsAny<string>(),
                It.IsAny<AdminMatchResultDto>(),
                It.IsAny<string>()),
            Times.Never);
    }

    // 1. TechnicalWin

    [Fact]
    public async Task TechnicalWin_ValidResult_ShouldSucceed()
    {
        var dto = ValidDto();

        var result = await _service.SetTechnicalResultAsync(
            MatchId, dto, AdministratorId);

        Assert.NotNull(result);
        VerifyResultCalledOnce();
    }

    // 2. Forfeit

    [Fact]
    public async Task Forfeit_ValidResult_ShouldSucceed()
    {
        var dto = ValidDto(MatchResultType.Forfeit);

        var result = await _service.SetTechnicalResultAsync(
            MatchId, dto, AdministratorId);

        Assert.NotNull(result);
        VerifyResultCalledOnce();
    }

    // 3. Disqualification

    [Fact]
    public async Task Disqualification_ValidResult_ShouldSucceed()
    {
        var dto = ValidDto(MatchResultType.Disqualification);

        var result = await _service.SetTechnicalResultAsync(
            MatchId, dto, AdministratorId);

        Assert.NotNull(result);
        VerifyResultCalledOnce();
    }

    // 4. Normal is not an administrative result

    [Fact]
    public async Task NormalResult_ShouldBeRejected()
    {
        var dto = ValidDto(MatchResultType.Normal);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 5. Invalid enum value

    [Fact]
    public async Task UnknownResultType_ShouldBeRejected()
    {
        var dto = ValidDto((MatchResultType)999);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 6. Missing administrator

    [Fact]
    public async Task MissingAdministrator_ShouldBeRejected()
    {
        var dto = ValidDto();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, ""));

        VerifyResultNeverCalled();
    }

    // 7. Missing reason

    [Fact]
    public async Task EmptyReason_ShouldBeRejected()
    {
        var dto = ValidDto();
        dto.Reason = "";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 8. Reason too short

    [Fact]
    public async Task ShortReason_ShouldBeRejected()
    {
        var dto = ValidDto();
        dto.Reason = "No";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 9. Match not found

    [Fact]
    public async Task MissingMatch_ShouldThrowNotFound()
    {
        _matches
    .Setup(r => r.GetByIdAsync(MatchId))
    .Returns(Task.FromResult<Match?>(null));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, ValidDto(), AdministratorId));

        VerifyResultNeverCalled();
    }

    // 10. Tournament not found

    [Fact]
    public async Task MissingTournament_ShouldThrowNotFound()
    {
        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync((Tournament?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, ValidDto(), AdministratorId));

        VerifyResultNeverCalled();
    }

    // 11. Tournament not in progress

    [Fact]
    public async Task DraftTournament_ShouldRejectResult()
    {
        ConfigureValidTournament(TournamentStatus.Draft);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, ValidDto(), AdministratorId));

        VerifyResultNeverCalled();
    }

    // 12. Already completed match

    [Fact]
    public async Task CompletedMatch_ShouldRejectResult()
    {
        ConfigureValidMatch(MatchStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, ValidDto(), AdministratorId));

        VerifyResultNeverCalled();
    }

    // 13. Cancelled match

    [Fact]
    public async Task CancelledMatch_ShouldRejectResult()
    {
        ConfigureValidMatch(MatchStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, ValidDto(), AdministratorId));

        VerifyResultNeverCalled();
    }

    // 14. Winner is not a participant

    [Fact]
    public async Task UnknownWinner_ShouldBeRejected()
    {
        var dto = ValidDto();
        dto.WinnerTeamId = "507f1f77bcf86cd799439099";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 15. Technical draw

    [Fact]
    public async Task TechnicalDraw_ShouldBeRejected()
    {
        var dto = ValidDto();
        dto.HomeScore = 2;
        dto.AwayScore = 2;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 16. Winner does not match score

    [Fact]
    public async Task WinnerScoreMismatch_ShouldBeRejected()
    {
        var dto = ValidDto();
        dto.WinnerTeamId = AwayTeamId;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetTechnicalResultAsync(
                MatchId, dto, AdministratorId));

        VerifyResultNeverCalled();
    }

    // 17. Away team can win

    [Fact]
    public async Task AwayTeamTechnicalWin_ShouldSucceed()
    {
        var dto = ValidDto();
        dto.WinnerTeamId = AwayTeamId;
        dto.HomeScore = 0;
        dto.AwayScore = 3;

        var result = await _service.SetTechnicalResultAsync(
            MatchId, dto, AdministratorId);

        Assert.NotNull(result);
        VerifyResultCalledOnce();
    }

    // 18. Delegate the exact administrative data

    [Fact]
    public async Task ValidResult_ShouldForwardAllAdministrativeData()
    {
        var dto = ValidDto(MatchResultType.Disqualification);

        await _service.SetTechnicalResultAsync(
            MatchId, dto, AdministratorId);

        _results.Verify(r => r.SetAdministrativeResultAsync(
            MatchId,
            It.Is<AdminMatchResultDto>(d =>
                d.ResultType == MatchResultType.Disqualification &&
                d.WinnerTeamId == HomeTeamId &&
                d.HomeScore == 3 &&
                d.AwayScore == 0 &&
                d.Reason == dto.Reason),
            AdministratorId),
            Times.Once);
    }
}
