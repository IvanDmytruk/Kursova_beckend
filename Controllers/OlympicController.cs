using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Olympic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Beckend.Controllers;

[ApiController]
[Authorize]
[Route("api/tournaments/{tournamentId}/olympic")]
public class OlympicController : ControllerBase
{
    private readonly IOlympicBracketService _service;
    private readonly ITournamentRepository _tournaments;

    public OlympicController(
        IOlympicBracketService service,
        ITournamentRepository tournaments)
    {
        _service = service;
        _tournaments = tournaments;
    }

    [HttpPost("generate-first-round")]
    public async Task<IActionResult> GenerateFirstRound(
        string tournamentId)
    {
        var accessError = await CheckAccessAsync(tournamentId);

        if (accessError != null)
            return accessError;

        try
        {
            var result = await _service
                .GenerateFirstRoundAsync(tournamentId);

            return Ok(result);
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
        catch (MongoWriteException ex)
            when (ex.WriteError.Category ==
                  ServerErrorCategory.DuplicateKey)
        {
            return Conflict(new
            {
                message = "Olympic bracket already exists."
            });
        }
    }

    [HttpPost("generate-next-round")]
    public async Task<IActionResult> GenerateNextRound(
        string tournamentId)
    {
        var accessError = await CheckAccessAsync(tournamentId);

        if (accessError != null)
            return accessError;

        try
        {
            var result = await _service
                .GenerateNextRoundAsync(tournamentId);

            return Ok(result);
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

    private async Task<IActionResult?> CheckAccessAsync(
        string tournamentId)
    {
        string? userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var tournament =
            await _tournaments.GetByIdAsync(tournamentId);

        if (tournament == null)
            return NotFound(new
            {
                message = "Tournament not found."
            });

        if (tournament.CreatedBy != userId &&
            !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        return null;
    }
}
