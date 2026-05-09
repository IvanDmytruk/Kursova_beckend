using Microsoft.AspNetCore.Mvc;
using Beckend.Services;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly SearchService _searchService;

        public SearchController(SearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("suggest")]
        public async Task<IActionResult> GetSuggestions([FromQuery] string q, [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Ok(new { query = q, suggestions = new List<object>() });
            }

            var result = await _searchService.GetSuggestionsAsync(q, limit);
            return Ok(result);
        }

        [HttpPost("increment-popularity")]
        public async Task<IActionResult> IncrementPopularity([FromBody] IncrementRequest request)
        {
            try
            {
                await _searchService.IncrementPopularityAsync(request.Id, request.Type);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class IncrementRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}