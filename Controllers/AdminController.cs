using Beckend.Enums;
using Beckend.Models;
using Beckend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<RoleRequest> _roleRequests;
        private readonly IMongoCollection<UserBan> _userBans;
        private readonly IMongoCollection<Tournament> _tournaments;
        private readonly IMongoCollection<Team> _teams;
        private readonly IMongoCollection<Match> _matches;

        public AdminController(IMongoClient client)
        {
            var database = client.GetDatabase("CreateadTournament");
            _users = database.GetCollection<User>("Users");
            _roleRequests = database.GetCollection<RoleRequest>("RoleRequests");
            _userBans = database.GetCollection<UserBan>("UserBans");
            _tournaments = database.GetCollection<Tournament>("Tournaments");
            _teams = database.GetCollection<Team>("Teams");
            _matches = database.GetCollection<Match>("Matches");
        }

        private string GetAdminId() => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";

        // GET: api/Admin/stats - статистика системи
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = new
            {
                TotalUsers = await _users.CountDocumentsAsync(_ => true),
                TotalTournaments = await _tournaments.CountDocumentsAsync(_ => true),
                TotalTeams = await _teams.CountDocumentsAsync(_ => true),
                TotalMatches = await _matches.CountDocumentsAsync(_ => true),
                PendingRoleRequests = await _roleRequests.CountDocumentsAsync(r => r.Status == "Pending")
            };
            return Ok(stats);
        }

        // GET: api/Admin/users - список всіх користувачів
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var users = await _users.Find(_ => true)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var total = await _users.CountDocumentsAsync(_ => true);

            return Ok(new { users, total, page, pageSize });
        }

        // PUT: api/Admin/users/{id}/role - зміна ролі користувача
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> ChangeUserRole(string id, [FromBody] string newRole)
        {
            var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

            if (!Enum.TryParse<UserRole>(newRole, true, out var role))
                return BadRequest("Invalid role");

            user.Role = role;
            await _users.ReplaceOneAsync(u => u.Id == id, user);

            return Ok(new { message = "Role updated successfully", newRole = user.Role.ToString() });
        }

        // POST: api/Admin/users/{id}/ban - блокування користувача
        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> BanUser(string id, [FromBody] BanRequest request)
        {
            var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

            var adminId = GetAdminId();
            var banDuration = request.Minutes switch
            {
                5 => TimeSpan.FromMinutes(5),
                10 => TimeSpan.FromMinutes(10),
                30 => TimeSpan.FromMinutes(30),
                60 => TimeSpan.FromMinutes(60),
                _ => TimeSpan.FromMinutes(5)
            };

            user.IsBanned = true;
            user.BannedUntil = DateTime.UtcNow.Add(banDuration);
            await _users.ReplaceOneAsync(u => u.Id == id, user);

            var banRecord = new UserBan
            {
                UserId = id,
                BannedUntil = user.BannedUntil.Value,
                Reason = request.Reason ?? "No reason",
                BannedBy = adminId,
                IsActive = true
            };
            await _userBans.InsertOneAsync(banRecord);

            return Ok(new { message = $"User banned until {user.BannedUntil}" });
        }

        // DELETE: api/Admin/users/{id}/unban - розблокування користувача
        [HttpDelete("users/{id}/unban")]
        public async Task<IActionResult> UnbanUser(string id)
        {
            var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

            user.IsBanned = false;
            user.BannedUntil = null;
            await _users.ReplaceOneAsync(u => u.Id == id, user);

            return Ok(new { message = "User unbanned successfully" });
        }

        // GET: api/Admin/role-requests - список запитів на роль
        [HttpGet("role-requests")]
        public async Task<IActionResult> GetRoleRequests([FromQuery] string status = "Pending")
        {
            var requests = await _roleRequests.Find(r => r.Status == status).ToListAsync();
            return Ok(requests);
        }

        // PUT: api/Admin/role-requests/{id}/approve - підтвердити запит
        [HttpPut("role-requests/{id}/approve")]
        public async Task<IActionResult> ApproveRoleRequest(string id)
        {
            var request = await _roleRequests.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (request == null)
                return NotFound("Request not found");

            var user = await _users.Find(u => u.Id == request.UserId).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

            if (!Enum.TryParse<UserRole>(request.RequestedRole, true, out var newRole))
                return BadRequest("Invalid role");

            user.Role = newRole;
            await _users.ReplaceOneAsync(u => u.Id == request.UserId, user);

            request.Status = "Approved";
            await _roleRequests.ReplaceOneAsync(r => r.Id == id, request);

            return Ok(new { message = $"Role request approved. User is now {newRole}" });
        }

        // DELETE: api/Admin/role-requests/{id}/reject - відхилити запит
        [HttpDelete("role-requests/{id}/reject")]
        public async Task<IActionResult> RejectRoleRequest(string id)
        {
            var request = await _roleRequests.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (request == null)
                return NotFound("Request not found");

            request.Status = "Rejected";
            await _roleRequests.ReplaceOneAsync(r => r.Id == id, request);

            return Ok(new { message = "Role request rejected" });
        }

        // GET: api/Admin/players/stats - статистика гравців (топ)
        [HttpGet("players/stats")]
        public async Task<IActionResult> GetPlayersStats([FromQuery] int limit = 10)
        {
            var players = await _users.Find(u => u.Role == UserRole.Player).ToListAsync();

            var stats = players.Select(p => new
            {
                p.Id,
                p.Name,
                p.Surname,
                Wins = 0, 
                Losses = 0,
                Points = 0,
                MatchesPlayed = 0
            }).OrderByDescending(s => s.Points).Take(limit);

            return Ok(stats);
        }
    }

    public class BanRequest
    {
        public int Minutes { get; set; } = 5;
        public string Reason { get; set; } = string.Empty;
    }
}