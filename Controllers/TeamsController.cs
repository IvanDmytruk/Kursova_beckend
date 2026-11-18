using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Beckend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<TeamMember> _members;
    private readonly IMongoCollection<Tournament> _tournaments;
    private readonly IMongoCollection<User> _users;

    public TeamsController(
        ITeamService teamService,
        IMongoClient client,
        IMongoDatabase database)
    {
        _teamService = teamService;
        _client = client;

        _teams = database.GetCollection<Team>("Teams");
        _members = database.GetCollection<TeamMember>("TeamMembers");
        _tournaments = database.GetCollection<Tournament>("Tournaments");
        _users = database.GetCollection<User>("Users");
    }

    private string? Actor =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    private bool IsAdmin => User.IsInRole("Admin");

    private static bool ValidId(string id) =>
        ObjectId.TryParse(id, out _);

    private async Task<Team?> FindTeam(string id)
    {
        if (!ValidId(id)) return null;

        return await _teams.Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _teams.Find(
            x => !x.IsArchived
        ).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var team = await FindTeam(id);

        if (team == null || team.IsArchived)
            return NotFound();

        return Ok(team);
    }

    [HttpGet("byname/{name}")]
    public async Task<IActionResult> GetByName(string name)
    {
        var team = await _teams.Find(x =>
            x.TeamName == name && !x.IsArchived
        ).FirstOrDefaultAsync();

        return team == null ? NotFound() : Ok(team);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Ok(Array.Empty<Team>());

        var escaped =
            System.Text.RegularExpressions.Regex.Escape(term);

        var filter = Builders<Team>.Filter.And(
            Builders<Team>.Filter.Eq(
                x => x.IsArchived, false),
            Builders<Team>.Filter.Regex(
                x => x.TeamName,
                new BsonRegularExpression(escaped, "i"))
        );

        return Ok(await _teams.Find(filter)
            .Limit(100)
            .ToListAsync());
    }

    [HttpGet("exists/{name}")]
    public async Task<IActionResult> Exists(string name)
    {
        var exists = await _teams.Find(x =>
            x.TeamName == name && !x.IsArchived
        ).AnyAsync();

        return Ok(new { name, exists });
    }

    [HttpGet("paged")]
    public async Task<IActionResult> Paged(
        int page = 1,
        int pageSize = 12)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var teams = await _teams.Find(x => !x.IsArchived)
            .SortBy(x => x.TeamName)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(teams);
    }


    [HttpGet("{id}/players")]
    public async Task<IActionResult> GetPlayers(string id)
    {
        var team = await FindTeam(id);

        if (team == null || team.IsArchived)
            return NotFound();

        var memberships = await _members.Find(m =>
            m.TeamId == id && m.IsActive
        ).ToListAsync();

        var ids = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        var users = await _users.Find(
            Builders<User>.Filter.In(
                u => u.Id, ids)
        ).ToListAsync();

        var map = users.ToDictionary(u => u.Id);

        var result = memberships.Select(m =>
        {
            map.TryGetValue(m.UserId, out var user);

            return new
            {
                m.UserId,
                m.Role,
                m.JoinedAt,
                IsOwner = m.UserId == team.OwnerId,
                Name = user?.Name,
                Surname = user?.Surname,
                Age = user?.Age
            };
        });

        return Ok(result);
    }


    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTeamDto dto,
        CancellationToken ct)
    {
        var actor = Actor;

        if (string.IsNullOrWhiteSpace(actor))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.TeamName) ||
            dto.TeamName.Trim().Length < 3)
        {
            return BadRequest(new
            {
                message = "Team name must contain at least 3 characters"
            });
        }

        if (!Enum.TryParse<SportName>(
                dto.SportName, true, out var sport) ||
            !Enum.IsDefined(sport))
        {
            return BadRequest(new
            {
                message = "Invalid sport"
            });
        }

        var name = dto.TeamName.Trim();

        if (name.Length > 100 ||
            (dto.TeamDescription?.Length ?? 0) > 2000)
        {
            return BadRequest(new
            {
                message = "Team name or description is too long"
            });
        }

        var existingName = await _teams.Find(
            x => x.TeamName == name && !x.IsArchived
        ).AnyAsync(ct);

        if (existingName)
            return Conflict(new
            {
                message = "Team name already exists"
            });

        var team = new Team
        {
            TeamName = name,
            TeamDescription = dto.TeamDescription?.Trim() ?? "",
            SportName = sport,
            OwnerId = actor,
            CreatedAt = DateTime.UtcNow
        };

        var ownerMembership = new TeamMember
        {
            TeamId = team.Id,
            UserId = actor,
            Role = TeamRole.Captain,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        try
        {
            using var session = await _client
                .StartSessionAsync(cancellationToken: ct);

            await session.WithTransactionAsync(
                async (s, token) =>
                {
                    await _teams.InsertOneAsync(
                        s, team,
                        cancellationToken: token);

                    await _members.InsertOneAsync(
                        s, ownerMembership,
                        cancellationToken: token);

                    return true;
                },
                cancellationToken: ct);

            return CreatedAtAction(
                nameof(GetById),
                new { id = team.Id },
                team);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError.Category ==
                  ServerErrorCategory.DuplicateKey)
        {
            return Conflict(new
            {
                message = "Duplicate team or membership"
            });
        }
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateTeamDto dto)
    {
        var actor = Actor;

        if (string.IsNullOrWhiteSpace(actor))
            return Unauthorized();

        var team = await FindTeam(id);

        if (team == null || team.IsArchived)
            return NotFound();

        if (team.OwnerId != actor && !IsAdmin)
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.TeamName) ||
            dto.TeamName.Trim().Length < 3)
            return BadRequest("Invalid team name");

        if (dto.TeamName.Trim().Length > 100 ||
            (dto.TeamDescription?.Length ?? 0) > 2000)
            return BadRequest("Team fields are too long");

        if (!Enum.TryParse<SportName>(
                dto.SportName, true, out var sport) ||
            !Enum.IsDefined(sport))
            return BadRequest("Invalid sport");
        if (team.SportName != sport)
        {
            var used = await _tournaments.Find(t =>
                t.TournamentParticipants.Any(
                    p => p.Id == id)
            ).AnyAsync();

            if (used)
                return Conflict(new
                {
                    message =
                        "Cannot change sport of a registered team"
                });
        }

        var filter = Builders<Team>.Filter.And(
            Builders<Team>.Filter.Eq(x => x.Id, id),
            Builders<Team>.Filter.Eq(x => x.IsArchived, false),
            IsAdmin
                ? Builders<Team>.Filter.Empty
                : Builders<Team>.Filter.Eq(x => x.OwnerId, actor)
        );

        var update = Builders<Team>.Update
            .Set(x => x.TeamName, dto.TeamName.Trim())
            .Set(
                x => x.TeamDescription,
                dto.TeamDescription?.Trim() ?? "")
            .Set(x => x.SportName, sport);

        var result = await _teams.UpdateOneAsync(
            filter, update);

        if (result.MatchedCount != 1)
            return Conflict("Team was modified or access changed");

        return Ok(await FindTeam(id));
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Archive(string id)
    {
        var actor = Actor;

        if (string.IsNullOrWhiteSpace(actor))
            return Unauthorized();

        var team = await FindTeam(id);

        if (team == null || team.IsArchived)
            return NotFound();

        if (team.OwnerId != actor && !IsAdmin)
            return Forbid();

        var hasActiveTournament = await _tournaments.Find(t =>
            t.TournamentParticipants.Any(p => p.Id == id) &&
            t.Status != TournamentStatus.Completed &&
            t.Status != TournamentStatus.Cancelled
        ).AnyAsync();

        if (hasActiveTournament)
            return Conflict(new
            {
                message =
                    "Team participates in an unfinished tournament"
            });

        var result = await _teams.UpdateOneAsync(
            x => x.Id == id &&
                 !x.IsArchived &&
                 (x.OwnerId == actor || IsAdmin),
            Builders<Team>.Update.Set(
                x => x.IsArchived, true)
        );

        return result.ModifiedCount == 1
            ? NoContent()
            : Conflict("Team could not be archived");
    }
}
