
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

public class InviteTeamMemberDto
{
    public string UserId { get; set; } = "";
    public TeamRole Role { get; set; } = TeamRole.Player;
}

public class ChangeTeamRoleDto
{
    public TeamRole Role { get; set; }
}

[ApiController]
[Authorize]
[Route("api/Teams")]
public class TeamMembershipController : ControllerBase
{
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<TeamMember> _members;
    private readonly IMongoCollection<TeamInvitation> _invitations;
    private readonly IMongoCollection<User> _users;
    private readonly TeamInvitationAcceptanceService _invitationAcceptance;

    public TeamMembershipController(IMongoDatabase db, TeamInvitationAcceptanceService invitationAcceptance)
    {
        _teams = db.GetCollection<Team>("Teams");
        _members = db.GetCollection<TeamMember>("TeamMembers");
        _invitations = db.GetCollection<TeamInvitation>(
            "TeamInvitations");
        _users = db.GetCollection<User>("Users");
        _invitationAcceptance = invitationAcceptance;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    private bool IsGlobalAdmin =>
        User.IsInRole("Admin");

    private async Task<bool> CanManageAsync(
        Team team,
        string userId)
    {
        if (team.OwnerId == userId || IsGlobalAdmin)
            return true;

        return await _members.Find(m =>
            m.TeamId == team.Id &&
            m.UserId == userId &&
            m.IsActive &&
            (m.Role == TeamRole.Captain ||
             m.Role == TeamRole.Coach)
        ).AnyAsync();
    }

    private async Task<bool> IsOwnerAsync(
        Team team,
        string userId)
    {
        await Task.CompletedTask;
        return team.OwnerId == userId || IsGlobalAdmin;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyTeams()
    {
        var userId = CurrentUserId;
        if (userId == null) return Unauthorized();

        var memberships = await _members
            .Find(m => m.UserId == userId && m.IsActive)
            .ToListAsync();

        var ids = memberships
            .Select(m => m.TeamId)
            .Distinct()
            .ToList();

        var filter = Builders<Team>.Filter.Or(
            Builders<Team>.Filter.Eq(t => t.OwnerId, userId),
            Builders<Team>.Filter.In(t => t.Id, ids)
        );

        var teams = await _teams.Find(filter).ToListAsync();

        return Ok(teams);
    }

    [HttpGet("{teamId}/members")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMembers(string teamId)
    {
        var team = await _teams.Find(t => t.Id == teamId)
            .FirstOrDefaultAsync();

        if (team == null) return NotFound();

        var memberships = await _members.Find(m =>
            m.TeamId == teamId && m.IsActive
        ).ToListAsync();

        var ids = memberships.Select(m => m.UserId)
            .Distinct()
            .ToList();

        var users = await _users.Find(
            Builders<User>.Filter.In(u => u.Id, ids)
        ).ToListAsync();

        var names = users.ToDictionary(u => u.Id);

        var result = memberships.Select(m =>
        {
            names.TryGetValue(m.UserId, out var user);

            return new
            {
                m.Id,
                m.TeamId,
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

    [HttpPost("{teamId}/invitations")]
    public async Task<IActionResult> Invite(
        string teamId,
        [FromBody] InviteTeamMemberDto dto)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        var team = await _teams.Find(t => t.Id == teamId)
            .FirstOrDefaultAsync();

        if (team == null) return NotFound();

        if (!await CanManageAsync(team, actor))
            return Forbid();

        if (!ObjectId.TryParse(dto.UserId, out _))
            return BadRequest("Invalid UserId");

        if (!Enum.IsDefined(dto.Role))
            return BadRequest("Invalid team role");

        var userExists = await _users
            .Find(u => u.Id == dto.UserId)
            .AnyAsync();

        if (!userExists)
            return NotFound("User not found");

        var alreadyMember = await _members.Find(m =>
            m.TeamId == teamId &&
            m.UserId == dto.UserId &&
            m.IsActive
        ).AnyAsync();

        if (alreadyMember)
            return Conflict("User is already a team member");

        var pending = await _invitations.Find(i =>
            i.TeamId == teamId &&
            i.UserId == dto.UserId &&
            i.Status == TeamInvitationStatus.Pending
        ).AnyAsync();

        if (pending)
            return Conflict("Invitation already exists");

        var invitation = new TeamInvitation
        {
            TeamId = teamId,
            UserId = dto.UserId,
            Role = dto.Role,
            CreatedByUserId = actor
        };

        await _invitations.InsertOneAsync(invitation);

        return Ok(invitation);
    }

    [HttpGet("invitations/my")]
    public async Task<IActionResult> MyInvitations()
    {
        var userId = CurrentUserId;
        if (userId == null) return Unauthorized();

        var invitations = await _invitations.Find(i =>
            i.UserId == userId &&
            i.Status == TeamInvitationStatus.Pending
        ).SortByDescending(i => i.CreatedAt).ToListAsync();

        return Ok(invitations);
    }

    [HttpGet("{teamId}/invitations")]
    public async Task<IActionResult> TeamInvitations(string teamId)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        var team = await _teams.Find(t => t.Id == teamId)
            .FirstOrDefaultAsync();

        if (team == null) return NotFound();

        if (!await CanManageAsync(team, actor))
            return Forbid();

        var invitations = await _invitations.Find(i =>
            i.TeamId == teamId
        ).SortByDescending(i => i.CreatedAt).ToListAsync();

        return Ok(invitations);
    }



    [HttpPatch("invitations/{id}/accept")]
    public async Task<IActionResult> Accept(
        string id,
        CancellationToken ct)
    {
        var actor = CurrentUserId;

        if (string.IsNullOrWhiteSpace(actor))
            return Unauthorized();

        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        {
            return BadRequest(new
            {
                message = "Invalid invitation ID"
            });
        }

        try
        {
            await _invitationAcceptance.AcceptAsync(
                id,
                actor,
                ct
            );

            return Ok(new
            {
                message = "Invitation accepted"
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
        catch (MongoDB.Driver.MongoWriteException ex)
            when (ex.WriteError.Category ==
                  MongoDB.Driver.ServerErrorCategory.DuplicateKey)
        {
            return Conflict(new
            {
                message = "User is already a team member"
            });
        }
    }


    [HttpPatch("invitations/{id}/reject")]
    public async Task<IActionResult> Reject(string id)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        var result = await _invitations.UpdateOneAsync(
            i => i.Id == id &&
                 i.UserId == actor &&
                 i.Status == TeamInvitationStatus.Pending,
            Builders<TeamInvitation>.Update
                .Set(i => i.Status, TeamInvitationStatus.Rejected)
                .Set(i => i.RespondedAt, DateTime.UtcNow)
        );

        if (result.ModifiedCount == 0)
            return NotFound("Pending invitation not found");

        return Ok(new { message = "Invitation rejected" });
    }

    [HttpPatch("invitations/{id}/cancel")]
    public async Task<IActionResult> CancelInvitation(string id)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        var invitation = await _invitations
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();

        if (invitation == null) return NotFound();

        var team = await _teams
            .Find(t => t.Id == invitation.TeamId)
            .FirstOrDefaultAsync();

        if (team == null) return NotFound();

        if (!await CanManageAsync(team, actor))
            return Forbid();

        var result = await _invitations.UpdateOneAsync(
            i => i.Id == id &&
                 i.Status == TeamInvitationStatus.Pending,
            Builders<TeamInvitation>.Update
                .Set(i => i.Status, TeamInvitationStatus.Cancelled)
                .Set(i => i.RespondedAt, DateTime.UtcNow)
        );

        if (result.ModifiedCount == 0)
            return Conflict("Invitation is not pending");

        return NoContent();
    }

    [HttpPatch("{teamId}/members/{userId}")]
    public async Task<IActionResult> ChangeRole(
        string teamId,
        string userId,
        [FromBody] ChangeTeamRoleDto dto)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        var team = await _teams.Find(t => t.Id == teamId)
            .FirstOrDefaultAsync();

        if (team == null) return NotFound();

        if (!await IsOwnerAsync(team, actor))
            return Forbid();

        if (team.OwnerId == userId)
            return Conflict("Owner role cannot be changed");

        if (!Enum.IsDefined(dto.Role))
            return BadRequest("Invalid role");

        var result = await _members.UpdateOneAsync(
            m => m.TeamId == teamId &&
                 m.UserId == userId &&
                 m.IsActive,
            Builders<TeamMember>.Update
                .Set(m => m.Role, dto.Role)
        );

        if (result.MatchedCount == 0)
            return NotFound("Active member not found");

        return NoContent();
    }

    [HttpDelete("{teamId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(
        string teamId,
        string userId)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        var team = await _teams.Find(t => t.Id == teamId)
            .FirstOrDefaultAsync();

        if (team == null) return NotFound();

        if (team.OwnerId == userId)
            return Conflict("Owner cannot be removed");

        if (actor != userId &&
            !await CanManageAsync(team, actor))
            return Forbid();

        var result = await _members.UpdateOneAsync(
            m => m.TeamId == teamId &&
                 m.UserId == userId &&
                 m.IsActive,
            Builders<TeamMember>.Update
                .Set(m => m.IsActive, false)
                .Set(m => m.LeftAt, DateTime.UtcNow)
        );

        if (result.MatchedCount == 0)
            return NotFound("Member not found");

        return NoContent();
    }

    [HttpPost("{teamId}/leave")]
    public async Task<IActionResult> Leave(string teamId)
    {
        var actor = CurrentUserId;
        if (actor == null) return Unauthorized();

        return await RemoveMember(teamId, actor);
    }

    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string? term,
        CancellationToken ct)
    {
        var actor = CurrentUserId;

        if (string.IsNullOrWhiteSpace(actor))
            return Unauthorized();

        var search = term?.Trim() ?? "";

        if (search.Length < 2)
        {
            return BadRequest(new
            {
                message = "Введіть щонайменше 2 символи"
            });
        }

        if (search.Length > 60)
        {
            return BadRequest(new
            {
                message = "Пошуковий запит занадто довгий"
            });
        }

        var pattern =
            System.Text.RegularExpressions.Regex.Escape(search);

        var regex = new BsonRegularExpression(pattern, "i");

        var filter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Regex(u => u.Name, regex),
            Builders<User>.Filter.Regex(u => u.Surname, regex)
        );

        var users = await _users
            .Find(filter)
            .Limit(15)
            .ToListAsync(ct);

        var result = users
            .Where(u => u.Id != actor)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Surname
            })
            .ToList();

        return Ok(result);
    }

}
