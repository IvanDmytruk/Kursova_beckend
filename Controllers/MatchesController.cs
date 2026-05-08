using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly MatchService _matchService;

        public MatchesController(MatchService matchService)
        {
            _matchService = matchService;
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

        // ⭐ ОДИН метод для отримання майбутніх матчів (з підтримкою фільтрації за спортом)
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

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateMatchDto updateDto)
        {
            try
            {
                var updated = await _matchService.UpdateAsync(id, updateDto);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var deleted = await _matchService.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}