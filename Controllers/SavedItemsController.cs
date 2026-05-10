using Beckend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SavedItemsController : ControllerBase
    {
        private readonly IMongoCollection<UserSavedItems> _savedItems;

        public SavedItemsController(IMongoClient client)
        {
            var database = client.GetDatabase("CreateadTournament");
            _savedItems = database.GetCollection<UserSavedItems>("UserSavedItems");
        }

        private string GetUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirst("sub")?.Value;
            }
            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            }
            return userId ?? "unknown";
        }

        [HttpGet]
        public async Task<IActionResult> GetUserSavedItems()
        {
            var userId = GetUserId();
            if (userId == "unknown")
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var items = await _savedItems.Find(s => s.UserId == userId).FirstOrDefaultAsync();
            return Ok(items ?? new UserSavedItems { UserId = userId });
        }

        [HttpPost("matches/{matchId}")]
        public async Task<IActionResult> SaveMatch(string matchId)
        {
            var userId = GetUserId();
            if (userId == "unknown") return Unauthorized();

            var update = Builders<UserSavedItems>.Update.AddToSet(s => s.SavedMatches, matchId);
            var result = await _savedItems.UpdateOneAsync(
                s => s.UserId == userId,
                update,
                new UpdateOptions { IsUpsert = true }
            );
            return Ok(new { success = true });
        }

        [HttpDelete("matches/{matchId}")]
        public async Task<IActionResult> RemoveSavedMatch(string matchId)
        {
            var userId = GetUserId();
            if (userId == "unknown") return Unauthorized();

            var update = Builders<UserSavedItems>.Update.Pull(s => s.SavedMatches, matchId);
            await _savedItems.UpdateOneAsync(s => s.UserId == userId, update);
            return Ok(new { success = true });
        }
        [HttpPost("tournaments/{tournamentId}")]
        public async Task<IActionResult> SaveTournament(string tournamentId)
        {
            var userId = GetUserId();
            if (userId == "unknown") return Unauthorized();

            var update = Builders<UserSavedItems>.Update.AddToSet(s => s.SavedTournaments, tournamentId);
            await _savedItems.UpdateOneAsync(
                s => s.UserId == userId,
                update,
                new UpdateOptions { IsUpsert = true }
            );
            return Ok(new { success = true });
        }

        [HttpDelete("tournaments/{tournamentId}")]
        public async Task<IActionResult> RemoveSavedTournament(string tournamentId)
        {
            var userId = GetUserId();
            if (userId == "unknown") return Unauthorized();

            var update = Builders<UserSavedItems>.Update.Pull(s => s.SavedTournaments, tournamentId);
            await _savedItems.UpdateOneAsync(s => s.UserId == userId, update);
            return Ok(new { success = true });
        }
        [HttpPost("players/{playerId}")]
        public async Task<IActionResult> SavePlayer(string playerId)
        {
            var userId = GetUserId();
            if (userId == "unknown") return Unauthorized();

            var update = Builders<UserSavedItems>.Update.AddToSet(s => s.SavedPlayers, playerId);
            await _savedItems.UpdateOneAsync(
                s => s.UserId == userId,
                update,
                new UpdateOptions { IsUpsert = true }
            );
            return Ok(new { success = true });
        }

        [HttpDelete("players/{playerId}")]
        public async Task<IActionResult> RemoveSavedPlayer(string playerId)
        {
            var userId = GetUserId();
            if (userId == "unknown") return Unauthorized();

            var update = Builders<UserSavedItems>.Update.Pull(s => s.SavedPlayers, playerId);
            await _savedItems.UpdateOneAsync(s => s.UserId == userId, update);
            return Ok(new { success = true });
        }
    }
}