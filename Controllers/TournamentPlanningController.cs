using Beckend.DTOs.TournamentPlanning;
using Beckend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Beckend.Controllers;

[ApiController]
[Route("api/tournaments/planning")]
public class TournamentPlanningController : ControllerBase
{
    private readonly ITournamentPlanningService _planningService;

    public TournamentPlanningController(
        ITournamentPlanningService planningService)
    {
        _planningService = planningService;
    }

    [HttpPost("suggest")]
    public ActionResult<TournamentPlanningResponseDto> Suggest(
        [FromBody] TournamentPlanningRequestDto request)
    {
        try
        {
            var result = _planningService.Suggest(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
