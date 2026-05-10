using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services;
using AutoMapper;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
public class RegisterForTournamentRequest
{
    public string TeamId { get; set; } = string.Empty;
}
namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentsController : ControllerBase
    {
        private readonly TournamentService _tournamentService;
        private readonly IMapper _mapper;
        public TournamentsController(TournamentService tournamentService, IMapper mapper)
        {
            _tournamentService = tournamentService;
            _mapper = mapper;
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
        public async Task<IActionResult> GetActive([FromQuery] SportName? sport = null)
        {
            var tournaments = await _tournamentService.GetActiveTournamentsAsync(sport);
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
                var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                createDto.CreatedBy = userId;

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
        [HttpPost("{tournamentId}/register")]
        public async Task<IActionResult> RegisterForTournament(string tournamentId, [FromBody] RegisterForTournamentRequest request)
        {
            try
            {
                var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                var tournament = await _tournamentService.GetByIdAsync(tournamentId);
                if (tournament == null)
                    return NotFound("Tournament not found");

                if (tournament.Format == TournamentFormat.Individual.ToString())
                {
                    await _tournamentService.RegisterPlayerAsync(tournamentId, userId);
                    return Ok(new { message = "Player successfully registered for tournament" });
                }
                else if (tournament.Format == TournamentFormat.Command.ToString())
                {
                    if (string.IsNullOrEmpty(request?.TeamId))
                        return BadRequest("TeamId is required for team tournament");

                    await _tournamentService.RegisterTeamAsync(tournamentId, request.TeamId);
                    return Ok(new { message = "Team successfully registered for tournament" });
                }
                else
                {
                    return BadRequest("Unknown tournament format");
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
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