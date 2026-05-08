// TeamsController.cs
using Microsoft.AspNetCore.Mvc;
using Beckend.DTOs;
using Beckend.Services;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeamsController : ControllerBase
    {
        private readonly TeamService _teamService;

        public TeamsController(TeamService teamService)
        {
            _teamService = teamService;
        }

        [HttpGet("byname/{name}")]
        public async Task<IActionResult> GetByName(string name)
        {
            try
            {
                var team = await _teamService.GetTeamByNameAsync(name);
                return Ok(team);
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

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term)
        {
            var teams = await _teamService.SearchTeamsAsync(term);
            return Ok(teams);
        }

        [HttpGet("exists/{name}")]
        public async Task<IActionResult> CheckNameExists(string name)
        {
            var exists = await _teamService.TeamNameExistsAsync(name);
            return Ok(new { name = name, exists = exists });
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var teams = await _teamService.GetTeamsPagedAsync(page, pageSize);
            return Ok(teams);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var teams = await _teamService.GetAllAsync();
            return Ok(teams);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var team = await _teamService.GetByIdAsync(id);
            if (team == null)
                return NotFound();
            return Ok(team);
        }
        [HttpGet("{id}/players")]
        public async Task<IActionResult> GetPlayers(string id)
        {
            try
            {
                var players = await _teamService.GetPlayersByTeamIdAsync(id);
                return Ok(players);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Team with id {id} not found");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTeamDto createDto)
        {
            try
            {
                var created = await _teamService.CreateAsync(createDto);
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
        public async Task<IActionResult> Update(string id, [FromBody] UpdateTeamDto updateDto)
        {
            try
            {
                var updated = await _teamService.UpdateAsync(id, updateDto);
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
            var deleted = await _teamService.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}