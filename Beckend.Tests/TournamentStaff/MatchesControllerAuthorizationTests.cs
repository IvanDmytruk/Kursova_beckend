using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beckend.Controllers;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Matches;
using Beckend.Services.Matches.Generation;
using Beckend.Services.Matches.Results;
using Beckend.Services.Matches.Administration;
using Beckend.Services.TournamentAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Beckend.DTOs;
using Match = Beckend.Models.Match;

namespace Beckend.Tests.TournamentStaffTests;

public class MatchesControllerAuthorizationTests
{
    private const string TournamentId = "tournament-1";
    private const string ManagerId = "manager-1";
    private const string MatchId = "match-1";

    private readonly Mock<IMatchService> _matches = new();
    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<IMatchGenerationService> _generation = new();
    private readonly Mock<IMatchResultService> _results = new();
    private readonly Mock<IMatchAdministrationService> _administration = new();
    private readonly Mock<ITournamentAccessService> _access = new();

    private readonly MatchesController _controller;

    public MatchesControllerAuthorizationTests()
    {
        _controller = new MatchesController(
            _matches.Object,
            _tournaments.Object,
            _generation.Object,
            _results.Object,
            _administration.Object,
            _access.Object);

        SetUser(ManagerId);

        _tournaments
            .Setup(r => r.GetByIdAsync(TournamentId))
            .ReturnsAsync(new Tournament
            {
                Id = TournamentId,
                CreatedBy = "organizer-1"
            });
        _matches
        .Setup(s => s.GetByIdAsync(MatchId))
        .ReturnsAsync(new MatchDto
        {
            Id = MatchId,
            TournamentId = TournamentId
        });
    }

    private void SetUser(string? userId)
    {
        var claims = new List<Claim>();

        if (userId != null)
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.Sub, userId));
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    [Fact]
    public async Task Delete_WithoutPermission_Returns403()
    {
        _access
            .Setup(a => a.HasPermissionAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches))
            .ReturnsAsync(false);

        var result = await _controller.Delete(MatchId);

        Assert.IsType<ForbidResult>(result);

        _matches.Verify(
            s => s.DeleteAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_WithPermission_Returns204()
    {
        _access
            .Setup(a => a.HasPermissionAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageMatches))
            .ReturnsAsync(true);

        _matches
            .Setup(s => s.DeleteAsync(MatchId))
            .ReturnsAsync(true);

        var result = await _controller.Delete(MatchId);

        Assert.IsType<NoContentResult>(result);

        _matches.Verify(
            s => s.DeleteAsync(MatchId),
            Times.Once);
    }

    [Fact]
    public async Task Delete_NoUser_Returns401()
    {
        SetUser(null);

        var result = await _controller.Delete(MatchId);

        Assert.IsType<UnauthorizedResult>(result);

        _matches.Verify(
            s => s.DeleteAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateRoundRobin_WithoutSchedulePermission_Returns403()
    {
        _access
            .Setup(a => a.HasPermissionAsync(
                TournamentId,
                ManagerId,
                TournamentPermission.ManageSchedule))
            .ReturnsAsync(false);

        var result = await _controller.GenerateRoundRobin(
            TournamentId);

        Assert.IsType<ForbidResult>(result);

        _access.Verify(a => a.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ManageSchedule), Times.Once);
    }

    [Fact]
    public async Task GenerateSwiss_WithoutSchedulePermission_Returns403()
    {
        var result = await _controller.GenerateSwissRound(
            TournamentId);

        Assert.IsType<ForbidResult>(result);

        _access.Verify(a => a.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ManageSchedule), Times.Once);
    }

    [Fact]
    public async Task GenerateGroupStage_WithoutSchedulePermission_Returns403()
    {
        var result = await _controller.GenerateGroupStage(
            TournamentId);

        Assert.IsType<ForbidResult>(result);

        _access.Verify(a => a.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ManageSchedule), Times.Once);
    }

    [Fact]
    public async Task GenerateDoubleElimination_WithoutSchedulePermission_Returns403()
    {
        var result =
            await _controller.GenerateDoubleEliminationRound(
                TournamentId);

        Assert.IsType<ForbidResult>(result);

        _access.Verify(a => a.HasPermissionAsync(
            TournamentId,
            ManagerId,
            TournamentPermission.ManageSchedule), Times.Once);
    }
    
[Fact]
public async Task UpdateResult_WithoutApproveResults_Returns403()
{
    var dto = new UpdateMatchResultDto
    {
        HomeScore = 2,
        AwayScore = 1
    };

    var result = await _controller.UpdateResult(MatchId, dto);

    Assert.IsType<ForbidResult>(result);

    _access.Verify(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.ApproveResults), Times.Once);

    _results.Verify(r => r.UpdateResultAsync(
        It.IsAny<string>(),
        It.IsAny<UpdateMatchResultDto>()), Times.Never);
}

[Fact]
public async Task UpdateResult_WithApproveResults_Returns200()
{
    var dto = new UpdateMatchResultDto
    {
        HomeScore = 2,
        AwayScore = 1
    };

    _access.Setup(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.ApproveResults))
        .ReturnsAsync(true);

    _results.Setup(r => r.UpdateResultAsync(MatchId, dto))
        .ReturnsAsync(new MatchDto
        {
            Id = MatchId,
            TournamentId = TournamentId
        });

    var result = await _controller.UpdateResult(MatchId, dto);

    Assert.IsType<OkObjectResult>(result);

    _results.Verify(r => r.UpdateResultAsync(
        MatchId, dto), Times.Once);
}

[Fact]
public async Task SetTechnicalResult_WithoutPermission_Returns403()
{
    var dto = new AdminMatchResultDto
    {
        ResultType = MatchResultType.TechnicalWin
    };

    var result = await _controller.SetTechnicalResult(
        MatchId, dto);

    Assert.IsType<ForbidResult>(result);

    _access.Verify(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.SetTechnicalResults), Times.Once);

    _administration.Verify(a => a.SetTechnicalResultAsync(
        It.IsAny<string>(),
        It.IsAny<AdminMatchResultDto>(),
        It.IsAny<string>()), Times.Never);
}

[Fact]
public async Task SetTechnicalResult_WithPermission_Returns200()
{
    var dto = new AdminMatchResultDto
    {
        ResultType = MatchResultType.TechnicalWin
    };

    _access.Setup(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.SetTechnicalResults))
        .ReturnsAsync(true);

    _administration.Setup(a => a.SetTechnicalResultAsync(
        MatchId, dto, ManagerId))
        .ReturnsAsync(new MatchDto
        {
            Id = MatchId,
            TournamentId = TournamentId
        });

    var result = await _controller.SetTechnicalResult(
        MatchId, dto);

    Assert.IsType<OkObjectResult>(result);

    _administration.Verify(a => a.SetTechnicalResultAsync(
        MatchId, dto, ManagerId), Times.Once);
}

[Fact]
public async Task Disqualification_WithoutBothPermissions_Returns403()
{
    var dto = new AdminMatchResultDto
    {
        ResultType = MatchResultType.Disqualification
    };

    _access.Setup(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.SetTechnicalResults))
        .ReturnsAsync(true);

    var result = await _controller.SetTechnicalResult(
        MatchId, dto);

    Assert.IsType<ForbidResult>(result);

    _access.Verify(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.SetTechnicalResults |
        TournamentPermission.DisqualifyTeams), Times.Once);

    _administration.Verify(a => a.SetTechnicalResultAsync(
        It.IsAny<string>(),
        It.IsAny<AdminMatchResultDto>(),
        It.IsAny<string>()), Times.Never);
}

[Fact]
public async Task Disqualification_WithBothPermissions_Returns200()
{
    var dto = new AdminMatchResultDto
    {
        ResultType = MatchResultType.Disqualification
    };

    var required =
        TournamentPermission.SetTechnicalResults |
        TournamentPermission.DisqualifyTeams;

    _access.Setup(a => a.HasPermissionAsync(
        TournamentId, ManagerId, required))
        .ReturnsAsync(true);

    _administration.Setup(a => a.SetTechnicalResultAsync(
        MatchId, dto, ManagerId))
        .ReturnsAsync(new MatchDto
        {
            Id = MatchId,
            TournamentId = TournamentId
        });

    var result = await _controller.SetTechnicalResult(
        MatchId, dto);

    Assert.IsType<OkObjectResult>(result);

    _administration.Verify(a => a.SetTechnicalResultAsync(
        MatchId, dto, ManagerId), Times.Once);
}

[Fact]
public async Task SetTechnicalResult_NullBody_Returns400()
{
    var result = await _controller.SetTechnicalResult(
        MatchId, null!);

    Assert.IsType<BadRequestObjectResult>(result);

    _administration.Verify(a => a.SetTechnicalResultAsync(
        It.IsAny<string>(),
        It.IsAny<AdminMatchResultDto>(),
        It.IsAny<string>()), Times.Never);
}

[Fact]
public async Task Delete_ForbiddenByService_Returns409()
{
    _access.Setup(a => a.HasPermissionAsync(
        TournamentId,
        ManagerId,
        TournamentPermission.ManageMatches))
        .ReturnsAsync(true);

    _matches.Setup(m => m.DeleteAsync(MatchId))
        .ThrowsAsync(new InvalidOperationException(
            "Cannot delete a completed or cancelled match"));

    var result = await _controller.Delete(MatchId);

    Assert.IsType<ConflictObjectResult>(result);
}
}
