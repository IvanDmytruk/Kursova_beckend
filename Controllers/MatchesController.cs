using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Beckend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly MatchService _matchService;
        private readonly TournamentRepository _tournamentRepository;

        public MatchesController(
     MatchService matchService,
     TournamentRepository tournamentRepository)
        {
            _matchService = matchService;
            _tournamentRepository = tournamentRepository;
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

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMatchDto createDto)
        {
            try
            {
                var created = await _matchService.CreateAsync(createDto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
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

            if (tournament.CreatedBy != userId &&
                !User.IsInRole("Admin"))
                return Forbid();

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

            if (tournament.CreatedBy != userId &&
                !User.IsInRole("Admin"))
                return Forbid();

            var deleted = await _matchService.DeleteAsync(id);

            if (!deleted)
                return NotFound();

            return NoContent();
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

            if (tournament.CreatedBy != userId &&
                !User.IsInRole("Admin"))
                return Forbid();

            try
            {
                var matches = await _matchService
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

            if (tournament.CreatedBy != userId &&
                !User.IsInRole("Admin"))
                return Forbid();

            try
            {
                var result = await _matchService.UpdateResultAsync(id, dto);
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

            if (tournament.CreatedBy != userId && !User.IsInRole("Admin"))
                return Forbid();

            try
            {
                var matches = await _matchService
                    .GenerateSwissRoundAsync(tournamentId);

                return Ok(matches);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    }
}