
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beckend.DTOs.TournamentAdministration;
using Beckend.Services.TournamentAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beckend.Controllers;

[ApiController]
[Authorize]
[Route("api/tournaments/{tournamentId}/administration")]
public class TournamentAdministrationController : ControllerBase
{
    private readonly ITournamentAdministrationService _service;

    public TournamentAdministrationController(
        ITournamentAdministrationService service)
    {
        _service = service;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    [HttpPost("sanctions")]
    public async Task<IActionResult> IssueSanction(
        string tournamentId,
        [FromBody] IssuePlayerSanctionDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var sanction = await _service.IssueSanctionAsync(
                tournamentId, dto, CurrentUserId);

            return StatusCode(201, sanction);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPatch("sanctions/{sanctionId}/revoke")]
    public async Task<IActionResult> RevokeSanction(
        string tournamentId,
        string sanctionId,
        [FromBody] RevokeSanctionDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            await _service.RevokeSanctionAsync(
                tournamentId,
                sanctionId,
                dto.Reason,
                CurrentUserId);

            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("sanctions/player/{playerId}")]
    public async Task<IActionResult> GetPlayerSanctions(
        string tournamentId,
        string playerId)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var sanctions = await _service.GetPlayerSanctionsAsync(
                tournamentId, playerId, CurrentUserId);

            return Ok(sanctions);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit(string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var entries = await _service.GetAuditAsync(
                tournamentId, CurrentUserId);

            return Ok(entries);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("teams/disqualify")]
    public async Task<IActionResult> DisqualifyTeam(
        string tournamentId,
        [FromBody] DisqualifyTeamDto dto)
    {
        var administratorId =
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?.Value
            ?? User.FindFirst(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                ?.Value;

        if (string.IsNullOrWhiteSpace(administratorId))
            return Unauthorized();

        try
        {
            var result = await _service.DisqualifyTeamAsync(
                tournamentId,
                dto,
                administratorId);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }


    private IActionResult HandleError(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => Forbid(),
            KeyNotFoundException => NotFound(
                new { message = ex.Message }),
            ArgumentException => BadRequest(
                new { message = ex.Message }),
            InvalidOperationException => Conflict(
                new { message = ex.Message }),
            _ => throw ex
        };
    }
}
