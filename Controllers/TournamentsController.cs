using Microsoft.AspNetCore.Mvc;
using Beckend.Models;
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

        // Отримати турнір за назвою
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

        //Пошук турнірів
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term)
        {
            var tournaments = await _tournamentService.SearchTournamentsAsync(term);
            return Ok(tournaments);
        }

        //Отримати активні турніри
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var tournaments = await _tournamentService.GetActiveTournamentsAsync();
            return Ok(tournaments);
        }

        //Перевірити чи існує назва
        [HttpGet("exists/{name}")]
        public async Task<IActionResult> CheckNameExists(string name)
        {
            var exists = await _tournamentService.TournamentNameExistsAsync(name);
            return Ok(new { name = name, exists = exists });
        }

        // Інші стандартні методи
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
        public async Task<IActionResult> Create([FromBody] Tournament tournament)
        {
            try
            {
                var created = await _tournamentService.CreateAsync(tournament);
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
        public async Task<IActionResult> Update(string id, [FromBody] Tournament tournament)
        {
            try
            {
                var updated = await _tournamentService.UpdateAsync(id, tournament);
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

        [HttpPost("{tournamentId}/teams")]
        public async Task<IActionResult> AddTeam(string tournamentId, [FromBody] Team team)
        {
            try
            {
                var result = await _tournamentService.AddTeamToTournament(tournamentId, team);
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
        public async Task<IActionResult> RemoveTeam(string tournamentId, [FromBody] Team team)
        {
            try
            {
                var result = await _tournamentService.RemoveTeamFromTournament(tournamentId, team);
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
