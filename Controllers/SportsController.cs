using Microsoft.AspNetCore.Mvc;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SportsController : ControllerBase
    {
        private readonly SportService _sportService;

        public SportsController(SportService sportService)
        {
            _sportService = sportService;
        }

        // Отримати вид спорту за назвою
        [HttpGet("byname/{sportName}")]
        public async Task<IActionResult> GetByName(SportName sportName)
        {
            try
            {
                var sport = await _sportService.GetSportByNameAsync(sportName);
                return Ok(sport);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        // Отримати види спорту за типом
        [HttpGet("bytype/{type}")]
        public async Task<IActionResult> GetByType(TypeSport type)
        {
            var sports = await _sportService.GetSportsByTypeAsync(type);
            return Ok(sports);
        }

        // Отримати активні види спорту
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var sports = await _sportService.GetActiveSportsAsync();
            return Ok(sports);
        }

        // Пошук за описом
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term)
        {
            var sports = await _sportService.SearchSportsByDescriptionAsync(term);
            return Ok(sports);
        }

        // Перевірити чи існує вид спорту
        [HttpGet("exists/{sportName}")]
        public async Task<IActionResult> CheckExists(SportName sportName)
        {
            var exists = await _sportService.SportExistsAsync(sportName);
            return Ok(new { sportName = sportName, exists = exists });
        }

        // Активація/деактивація
        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            try
            {
                var sport = await _sportService.ToggleSportActiveAsync(id);
                return Ok(sport);
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
            var sports = await _sportService.GetAllAsync();
            return Ok(sports);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var sport = await _sportService.GetByIdAsync(id);
            if (sport == null)
                return NotFound();
            return Ok(sport);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Sport sport)
        {
            try
            {
                var created = await _sportService.CreateAsync(sport);
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
        public async Task<IActionResult> Update(string id, [FromBody] Sport sport)
        {
            try
            {
                var updated = await _sportService.UpdateAsync(id, sport);
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
            var deleted = await _sportService.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}