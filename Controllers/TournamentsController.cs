// TournamentsController.cs
using Microsoft.AspNetCore.Mvc;
using Beckend.DTOs;
using Beckend.Services;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentsController : ControllerBase
    {
        private readonly TournamentService _tournamentService;

        public TournamentsController(TournamentService tournamentService)
        {
            _tournamentService = tournamentService;
        }

        [HttpGet("byname/{name}")]
        public async Task<IActionResult> GetByName(string name)
        {
            try
            {
                var tournament = await _tournamentService.GetTournamentByNameAsync(name);
                return Ok(tournament);
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
            var tournaments = await _tournamentService.SearchTournamentsAsync(term);
            return Ok(tournaments);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var tournaments = await _tournamentService.GetActiveTournamentsAsync();
            return Ok(tournaments);
        }

        [HttpGet("exists/{name}")]
        public async Task<IActionResult> CheckNameExists(string name)
        {
            var exists = await _tournamentService.TournamentNameExistsAsync(name);
            return Ok(new { name = name, exists = exists });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tournaments = await _tournamentService.GetAllAsync();
            return Ok(tournaments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var tournament = await _tournamentService.GetByIdAsync(id);
            if (tournament == null)
                return NotFound();
            return Ok(tournament);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTournamentDto createDto)
        {
            try
            {
                var created = await _tournamentService.CreateAsync(createDto);
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
        public async Task<IActionResult> Update(string id, [FromBody] UpdateTournamentDto updateDto)
        {
            try
            {
                var updated = await _tournamentService.UpdateAsync(id, updateDto);
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
            var deleted = await _tournamentService.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }

        [HttpPost("{tournamentId}/teams/{teamId}")]
        public async Task<IActionResult> AddTeam(string tournamentId, string teamId)
        {
            try
            {
                var result = await _tournamentService.AddTeamToTournament(tournamentId, teamId);
                if (result)
                    return Ok();
                return BadRequest("Failed to add team");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{tournamentId}/teams/{teamId}")]
        public async Task<IActionResult> RemoveTeam(string tournamentId, string teamId)
        {
            try
            {
                var result = await _tournamentService.RemoveTeamFromTournament(tournamentId, teamId);
                if (result)
                    return Ok();
                return BadRequest("Failed to remove team");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}