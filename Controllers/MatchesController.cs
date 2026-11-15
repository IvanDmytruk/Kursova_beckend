using Beckend.DTOs;
using Beckend.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Matches;
using Beckend.Services.Matches.Generation;
using Beckend.Services.Matches.Results;
using Beckend.Services.Matches.Administration;
using Beckend.Services.TournamentAccess;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly ITournamentRepository _tournamentRepository;
        private readonly IMatchGenerationService _matchGenerationService;
        private readonly IMatchResultService _matchResultService;
        private readonly IMatchAdministrationService _administrationService;
        private readonly ITournamentAccessService _tournamentAccess;

        public MatchesController(
     IMatchService matchService,
     ITournamentRepository tournamentRepository,
     IMatchGenerationService matchGenerationService,
     IMatchResultService matchResultsService,
     IMatchAdministrationService administrationService,
     ITournamentAccessService tournamentAccess)
        {
            _matchService = matchService;
            _tournamentRepository = tournamentRepository;
            _matchGenerationService = matchGenerationService;
            _matchResultService = matchResultsService;
            _administrationService = administrationService;
            _tournamentAccess = tournamentAccess;
        }

        [HttpGet("byname/{name}")]
        public async Task<IActionResult> GetByName(string name)
        {
            try
            {
                var match = await _matchService.GetMatchByNameAsync(name);
                return Ok(match);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("tournament/{tournamentId}")]
        public async Task<IActionResult> GetByTournamentId(string tournamentId)
        {
            try
            {
                var matches = await _matchService.GetMatchesByTournamentIdAsync(tournamentId);
                return Ok(matches);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("team/{teamId}")]
        public async Task<IActionResult> GetByTeamId(string teamId)
        {
            try
            {
                var matches = await _matchService.GetMatchesByTeamIdAsync(teamId);
                return Ok(matches);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] SportName? sport = null)
        {
            var matches = await _matchService.GetUpcomingMatchesAsync(sport);
            return Ok(matches);
        }

        [HttpGet("daterange")]
        public async Task<IActionResult> GetByDateRange([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            try
            {
                var matches = await _matchService.GetMatchesByDateRangeAsync(start, end);
                return Ok(matches);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("startingsoon")]
        public async Task<IActionResult> GetStartingSoon([FromQuery] int hours = 24)
        {
            var matches = await _matchService.GetMatchesStartingSoonAsync(hours);
            return Ok(matches);
        }

        [HttpGet("exists/{name}")]
        public async Task<IActionResult> CheckExists(string name)
        {
            var exists = await _matchService.MatchExistsAsync(name);
            return Ok(new { name = name, exists = exists });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var matches = await _matchService.GetAllAsync();
            return Ok(matches);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var match = await _matchService.GetByIdAsync(id);
            if (match == null)
                return NotFound();
            return Ok(match);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateMatchDto createDto)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
                return Unauthorized();

            if (createDto == null ||
                string.IsNullOrWhiteSpace(createDto.TournamentId))
            {
                return BadRequest(new
                {
                    message = "TournamentId is required"
                });
            }

            var tournament = await _tournamentRepository
                .GetByIdAsync(createDto.TournamentId);

            if (tournament == null)
            {
                return NotFound(new
                {
                    message = "Tournament not found"
                });
            }

            if (!await CanManageTournamentAsync(
                    tournament.Id,
                    TournamentPermission.ManageMatches))
            {
                return Forbid();
            }

            try
            {
                var created = await _matchService.CreateAsync(createDto);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = created.Id },
                    created);
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

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            string id,
            [FromBody] UpdateMatchDto updateDto)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var match = await _matchService.GetByIdAsync(id);

            if (match == null)
                return NotFound(new { message = "Match not found" });

            var tournament = await _tournamentRepository
                .GetByIdAsync(match.TournamentId);

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            if (!await CanManageTournamentAsync( tournament.Id,TournamentPermission.ManageMatches))
            {
                return Forbid();
            }
            if (updateDto == null)
                return BadRequest(new { message = "Request body is required" });

            if (updateDto.TournamentId != match.TournamentId)
            {
                return BadRequest(new
                {
                    message = "Changing a match tournament is not allowed"
                });
            }
            try
            {
                var updated = await _matchService.UpdateAsync(id, updateDto);
                return Ok(updated);
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

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var match = await _matchService.GetByIdAsync(id);

            if (match == null)
                return NotFound(new { message = "Match not found" });

            var tournament = await _tournamentRepository
                .GetByIdAsync(match.TournamentId);

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            if (!await CanManageTournamentAsync( tournament.Id, TournamentPermission.ManageMatches))
            {
                return Forbid();
            }
            try
            {
                var deleted = await _matchService.DeleteAsync(id);

                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }


        [Authorize]
        [HttpPost("tournament/{tournamentId}/generate-round-robin")]
        public async Task<IActionResult> GenerateRoundRobin(
            string tournamentId)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });
            if (!await CanManageTournamentAsync(tournament.Id, TournamentPermission.ManageSchedule))
            {
                return Forbid();
            }

            try
            {
                var matches = await _matchGenerationService
                    .GenerateRoundRobinAsync(tournamentId);

                return Ok(matches);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
        [Authorize]
        [HttpPatch("{id}/result")]
        public async Task<IActionResult> UpdateResult(string id,[FromBody] UpdateMatchResultDto dto)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var match = await _matchService.GetByIdAsync(id);

            if (match == null)
                return NotFound();

            var tournament = await _tournamentRepository
                .GetByIdAsync(match.TournamentId);

            if (tournament == null)
                return NotFound();
            if (!await CanManageTournamentAsync(tournament.Id,TournamentPermission.ApproveResults))
            {
                return Forbid();
            }

            try
            {
                var result = await _matchResultService.UpdateResultAsync(id, dto);
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
        [Authorize]
        [HttpPost("tournament/{tournamentId}/generate-swiss-round")]
        public async Task<IActionResult> GenerateSwissRound(string tournamentId)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });
            if (!await CanManageTournamentAsync( tournament.Id,TournamentPermission.ManageSchedule))
            {
                return Forbid();
            }

            try
            {
                var matches = await _matchGenerationService
                    .GenerateSwissRoundAsync(tournamentId);

                return Ok(matches);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
        [Authorize]
        [HttpPost("tournament/{tournamentId}/generate-group-stage")]
        public async Task<IActionResult> GenerateGroupStage(string tournamentId)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                return NotFound(new
                {
                    message = "Tournament not found"
                });
            if (!await CanManageTournamentAsync(tournament.Id,TournamentPermission.ManageSchedule))
            {
                return Forbid();
            }

            try
            {
                var matches = await _matchGenerationService
                    .GenerateGroupStageAsync(tournamentId);

                return Ok(matches);
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

        [Authorize]
        [HttpPost("tournament/{tournamentId}/generate-double-elimination-round")]
        public async Task<IActionResult> GenerateDoubleEliminationRound(
            string tournamentId)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
            {
                return NotFound(new
                {
                    message = "Tournament not found"
                });
            }
            if (!await CanManageTournamentAsync(tournament.Id,TournamentPermission.ManageSchedule))
            {
                return Forbid();
            }

            try
            {
                var matches = await _matchGenerationService
                    .GenerateDoubleEliminationRoundAsync(tournamentId);

                return Ok(matches);
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

        [Authorize]
        [HttpPatch("{id}/technical-result")]
        public async Task<IActionResult> SetTechnicalResult(
            string id,
            [FromBody] AdminMatchResultDto dto)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var match = await _matchService.GetByIdAsync(id);

            if (match == null)
                return NotFound();

            var tournament = await _tournamentRepository
                .GetByIdAsync(match.TournamentId);

            if (tournament == null)
                return NotFound();

            if (dto == null)
                return BadRequest(new { message = "Request body is required" });

            var requiredPermission = dto.ResultType ==
                MatchResultType.Disqualification
                    ? TournamentPermission.SetTechnicalResults |
                      TournamentPermission.DisqualifyTeams
                    : TournamentPermission.SetTechnicalResults;

            if (!await CanManageTournamentAsync(
                tournament.Id,
                requiredPermission))
            {
                return Forbid();
            }

            try
            {
                var result = await _administrationService
                    .SetTechnicalResultAsync(id, dto, userId);

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

        private string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        private async Task<bool> CanManageTournamentAsync(
            string tournamentId,
            TournamentPermission permission)
        {
            var userId = CurrentUserId;

            if (string.IsNullOrWhiteSpace(userId))
                return false;

            if (User.IsInRole("Admin"))
                return true;

            return await _tournamentAccess.HasPermissionAsync(
                tournamentId,
                userId,
                permission);
        }

    }
}