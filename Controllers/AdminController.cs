using Beckend.Enums;
using Beckend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Match = Beckend.Models.Match;

namespace Beckend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<TeamMember> _teamMembers;
    private readonly IMongoCollection<RoleRequest> _roleRequests;
    private readonly IMongoCollection<UserBan> _userBans;
    private readonly IMongoCollection<Tournament> _tournaments;
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<Match> _matches;

    public AdminController(IMongoClient client)
    {
        var database = client.GetDatabase("CreateadTournament");

        _users = database.GetCollection<User>("Users");
        _teamMembers = database.GetCollection<TeamMember>("TeamMembers");
        _roleRequests = database.GetCollection<RoleRequest>("RoleRequests");
        _userBans = database.GetCollection<UserBan>("UserBans");
        _tournaments = database.GetCollection<Tournament>("Tournaments");
        _teams = database.GetCollection<Team>("Teams");
        _matches = database.GetCollection<Match>("Matches");
    }

    private string GetAdminId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? string.Empty;
    }

    private static bool TryParseGlobalRole(
        string? value,
        out UserRole role)
    {
        role = UserRole.User;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Enum.TryParse<UserRole>(
                value.Trim(),
                true,
                out var parsed))
            return false;

        if (parsed != UserRole.User &&
            parsed != UserRole.Admin)
            return false;

        role = parsed;
        return true;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = new
        {
            TotalUsers =
                await _users.CountDocumentsAsync(_ => true),

            TotalTournaments =
                await _tournaments.CountDocumentsAsync(_ => true),

            TotalTeams =
                await _teams.CountDocumentsAsync(_ => true),

            TotalMatches =
                await _matches.CountDocumentsAsync(_ => true),

            PendingRoleRequests =
                await _roleRequests.CountDocumentsAsync(
                    r => r.Status == "Pending"),

            ActiveTeamMembers =
                await _teamMembers.CountDocumentsAsync(
                    m => m.IsActive)
        };

        return Ok(stats);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(
                "Page must be >= 1 and pageSize between 1 and 100.");

        var users = await _users
            .Find(_ => true)
            .SortBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var total =
            await _users.CountDocumentsAsync(_ => true);

        var result = users.Select(u => new
        {
            u.Id,
            u.Name,
            u.Surname,
            u.Age,
            Role = u.Role.ToString(),
            Email = u.ContactInfo?.Email,
            Phone = u.ContactInfo?.Phone,
            u.IsBanned,
            u.BannedUntil
        });

        return Ok(new
        {
            users = result,
            total,
            page,
            pageSize
        });
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeUserRole(
        string id,
        [FromBody] string newRole)
    {
        if (!TryParseGlobalRole(newRole, out var role))
            return BadRequest(
                "Only User and Admin are valid global roles.");

        if (id == GetAdminId() && role != UserRole.Admin)
            return Conflict(
                "You cannot remove your own administrator role.");

        var result = await _users.UpdateOneAsync(
            u => u.Id == id,
            Builders<User>.Update.Set(u => u.Role, role));

        if (result.MatchedCount == 0)
            return NotFound("User not found");

        return Ok(new
        {
            message = "Role updated successfully",
            newRole = role.ToString()
        });
    }

    [HttpPost("users/{id}/ban")]
    public async Task<IActionResult> BanUser(
        string id,
        [FromBody] BanRequest request)
    {
        if (id == GetAdminId())
            return Conflict("You cannot ban yourself.");

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length < 5)
        {
            return BadRequest(
                "Ban reason must contain at least 5 characters.");
        }

        if (request.Minutes is not (5 or 10 or 30 or 60))
            return BadRequest(
                "Allowed ban durations: 5, 10, 30, 60 minutes.");

        var user = await _users
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound("User not found");

        var adminId = GetAdminId();

        if (string.IsNullOrWhiteSpace(adminId))
            return Unauthorized();

        var bannedUntil =
            DateTime.UtcNow.AddMinutes(request.Minutes);

        await _users.UpdateOneAsync(
            u => u.Id == id,
            Builders<User>.Update
                .Set(u => u.IsBanned, true)
                .Set(u => u.BannedUntil, bannedUntil));

        var banRecord = new UserBan
        {
            UserId = id,
            BannedUntil = bannedUntil,
            Reason = request.Reason.Trim(),
            BannedBy = adminId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userBans.InsertOneAsync(banRecord);

        return Ok(new
        {
            message = "User banned",
            bannedUntil
        });
    }

    [HttpDelete("users/{id}/unban")]
    public async Task<IActionResult> UnbanUser(string id)
    {
        var result = await _users.UpdateOneAsync(
            u => u.Id == id,
            Builders<User>.Update
                .Set(u => u.IsBanned, false)
                .Set(u => u.BannedUntil, null));

        if (result.MatchedCount == 0)
            return NotFound("User not found");

        await _userBans.UpdateManyAsync(
            b => b.UserId == id && b.IsActive,
            Builders<UserBan>.Update.Set(
                b => b.IsActive, false));

        return Ok(new
        {
            message = "User unbanned successfully"
        });
    }

    [HttpGet("role-requests")]
    public async Task<IActionResult> GetRoleRequests(
        [FromQuery] string status = "Pending")
    {
        var requests = await _roleRequests
            .Find(r => r.Status == status)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPut("role-requests/{id}/approve")]
    public async Task<IActionResult> ApproveRoleRequest(string id)
    {
        var request = await _roleRequests
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync();

        if (request == null)
            return NotFound("Request not found");

        if (request.Status != "Pending")
            return Conflict("Request is already processed.");

        if (!TryParseGlobalRole(
                request.RequestedRole,
                out var newRole))
        {
            return BadRequest(
                "Only User and Admin requests can be approved here.");
        }

        var user = await _users
            .Find(u => u.Id == request.UserId)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound("User not found");

        var claimed = await _roleRequests.UpdateOneAsync(
            r => r.Id == id && r.Status == "Pending",
            Builders<RoleRequest>.Update.Set(
                r => r.Status, "Processing"));

        if (claimed.ModifiedCount != 1)
            return Conflict("Request is already being processed.");

        try
        {
            var updateResult = await _users.UpdateOneAsync(
                u => u.Id == request.UserId,
                Builders<User>.Update.Set(
                    u => u.Role, newRole));

            if (updateResult.MatchedCount == 0)
                throw new InvalidOperationException(
                    "User no longer exists.");

            await _roleRequests.UpdateOneAsync(
                r => r.Id == id && r.Status == "Processing",
                Builders<RoleRequest>.Update.Set(
                    r => r.Status, "Approved"));
        }
        catch
        {
            await _roleRequests.UpdateOneAsync(
                r => r.Id == id && r.Status == "Processing",
                Builders<RoleRequest>.Update.Set(
                    r => r.Status, "Pending"));

            throw;
        }

        return Ok(new
        {
            message = "Role request approved",
            newRole = newRole.ToString()
        });
    }

    [HttpDelete("role-requests/{id}/reject")]
    public async Task<IActionResult> RejectRoleRequest(string id)
    {
        var result = await _roleRequests.UpdateOneAsync(
            r => r.Id == id && r.Status == "Pending",
            Builders<RoleRequest>.Update.Set(
                r => r.Status, "Rejected"));

        if (result.ModifiedCount == 1)
            return Ok(new
            {
                message = "Role request rejected"
            });

        var exists = await _roleRequests
            .Find(r => r.Id == id)
            .AnyAsync();

        return exists
            ? Conflict("Request is already processed.")
            : NotFound("Request not found");
    }

    [HttpGet("players/stats")]
    public async Task<IActionResult> GetPlayersStats(
        [FromQuery] int limit = 10)
    {
        if (limit < 1 || limit > 100)
            return BadRequest("Limit must be between 1 and 100.");

        var memberships = await _teamMembers
            .Find(m =>
                m.IsActive &&
                m.Role == TeamRole.Player)
            .ToListAsync();

        var userIds = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return Ok(Array.Empty<object>());

        var users = await _users
            .Find(Builders<User>.Filter.In(
                u => u.Id, userIds))
            .ToListAsync();

        var stats = users
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Surname,
                Wins = 0,
                Losses = 0,
                Points = 0,
                MatchesPlayed = 0
            })
            .OrderByDescending(s => s.Points)
            .ThenBy(s => s.Name)
            .Take(limit)
            .ToList();

        return Ok(stats);
    }
}
public class BanRequest
{
    public int Minutes { get; set; } = 5;
    public string Reason { get; set; } = string.Empty;
}
