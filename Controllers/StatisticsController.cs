using Microsoft.AspNetCore.Mvc;
using Beckend.Models;
using Beckend.Services;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly StatisticService _statisticService;

        public StatisticsController(StatisticService statisticService)
        {
            _statisticService = statisticService;
        }

        // Отримати статистику гравця
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(string userId)
        {
            try
            {
                var statistics = await _statisticService.GetStatisticsByUserIdAsync(userId);
                return Ok(statistics);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Отримати статистику команди
        [HttpGet("team/{teamId}")]
        public async Task<IActionResult> GetByTeamId(string teamId)
        {
            try
            {
                var statistics = await _statisticService.GetStatisticsByTeamIdAsync(teamId);
                return Ok(statistics);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Отримати статистику турніру
        [HttpGet("tournament/{tournamentId}")]
        public async Task<IActionResult> GetByTournamentId(string tournamentId)
        {
            try
            {
                var statistics = await _statisticService.GetStatisticsByTournamentIdAsync(tournamentId);
                return Ok(statistics);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Отримати статистику за сезоном
        [HttpGet("season/{season}")]
        public async Task<IActionResult> GetBySeason(string season)
        {
            var statistics = await _statisticService.GetStatisticsBySeasonAsync(season);
            return Ok(statistics);
        }

        // Отримати топ гравців
        [HttpGet("top")]
        public async Task<IActionResult> GetTopPlayers([FromQuery] int limit = 10)
        {
            var topPlayers = await _statisticService.GetTopPlayersAsync(limit);
            return Ok(topPlayers);
        }

        // Оновити статистику після матчу
        [HttpPatch("{id}/match")]
        public async Task<IActionResult> UpdateMatchStats(string id, [FromBody] MatchStatsRequest request)
        {
            try
            {
                var updated = await _statisticService.UpdateMatchStatsAsync(id, request.IsWin, request.IsDraw);
                return Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        // Стандартні CRUD методи
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var statistics = await _statisticService.GetAllAsync();
            return Ok(statistics);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var statistic = await _statisticService.GetByIdAsync(id);
            if (statistic == null)
                return NotFound();
            return Ok(statistic);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Statistic statistic)
        {
            try
            {
                var created = await _statisticService.CreateAsync(statistic);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] Statistic statistic)
        {
            try
            {
                var updated = await _statisticService.UpdateAsync(id, statistic);
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
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var deleted = await _statisticService.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }

    // DTO для запиту оновлення статистики матчу
    public class MatchStatsRequest
    {
        public bool IsWin { get; set; }
        public bool IsDraw { get; set; }
    }
}