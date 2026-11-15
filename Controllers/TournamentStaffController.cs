using System.Security.Claims;
using Beckend.DTOs.TournamentStaff;
using Beckend.Services.TournamentStaff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Beckend.Controllers;

[ApiController]
[Authorize]
[Route("api/tournaments/{tournamentId}/staff")]
public class TournamentStaffController : ControllerBase
{
    private readonly ITournamentStaffService _staffService;

    public TournamentStaffController(
        ITournamentStaffService staffService)
    {
        _staffService = staffService;
    }

    private string? CurrentUserId =>
    User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    [HttpPost]
    public async Task<IActionResult> AssignManager(
        string tournamentId,
        [FromBody] AssignManagerDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var manager = await _staffService.AssignManagerAsync(
                tournamentId,
                dto.UserId,
                dto.Permissions,
                CurrentUserId);

            return StatusCode(StatusCodes.Status201Created, manager);
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

    [HttpGet]
    public async Task<IActionResult> GetManagers(
        string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var managers = await _staffService.GetManagersAsync(
                tournamentId,
                CurrentUserId);

            return Ok(managers);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{userId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(
        string tournamentId,
        string userId,
        [FromBody] UpdateManagerPermissionsDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var updated =
                await _staffService.UpdateManagerPermissionsAsync(
                    tournamentId,
                    userId,
                    dto.Permissions,
                    CurrentUserId);

            return updated ? NoContent() : NotFound();
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
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> RevokeManager(
        string tournamentId,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        try
        {
            var revoked = await _staffService.RevokeManagerAsync(
                tournamentId,
                userId,
                CurrentUserId);

            return revoked ? NoContent() : NotFound();
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
    }
}
