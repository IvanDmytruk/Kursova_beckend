
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentAccess;
using Beckend.Services.Tournaments.Registration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Beckend.Controllers;

public class CreateParticipationRequestDto
{
    public string TeamId { get; set; } = "";
    public string PlayerId { get; set; } = "";
}

[ApiController]
[Authorize]
[Route("api/Tournaments")]
public class TournamentParticipationRequestsController
    : ControllerBase
{
    private readonly IMongoCollection<Tournament> _tournaments;
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<TeamMember> _members;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<TournamentParticipationRequest> _requests;
    private readonly ParticipationApprovalService _approval;

    private readonly ITournamentAccessService _access;
    private readonly ITournamentRegistrationService _registration;

    public TournamentParticipationRequestsController(
        IMongoDatabase db,
        ITournamentAccessService access,
        ITournamentRegistrationService registration,
        ParticipationApprovalService approval)
    {
        _tournaments = db.GetCollection<Tournament>("Tournaments");
        _teams = db.GetCollection<Team>("Teams");
        _members = db.GetCollection<TeamMember>("TeamMembers");
        _users = db.GetCollection<User>("Users");
        _requests = db.GetCollection<TournamentParticipationRequest>(
            "TournamentParticipationRequests");
        _approval = approval;

        _access = access;
        _registration = registration;
    }

    private string? Actor =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    private async Task<bool> CanManageTeam(
        string teamId,
        string actor)
    {
        var team = await _teams.Find(t => t.Id == teamId)
            .FirstOrDefaultAsync();

        if (team == null) return false;

        if (team.OwnerId == actor)
            return true;

        return await _members.Find(m =>
            m.TeamId == teamId &&
            m.UserId == actor &&
            m.IsActive &&
            (m.Role == TeamRole.Captain ||
             m.Role == TeamRole.Coach)
        ).AnyAsync();
    }

    private async Task<bool> CanManageTournament(
        string tournamentId,
        string actor)
    {
        return await _access.HasPermissionAsync(
            tournamentId,
            actor,
            TournamentPermission.ManageParticipants);
    }

    private async Task<bool> IsTargetAuthorized(
        TournamentParticipationRequest request,
        string actor)
    {
        if (!string.IsNullOrWhiteSpace(request.TeamId))
            return await CanManageTeam(request.TeamId, actor);

        return request.PlayerId == actor;
    }

    private async Task<IActionResult?> ValidateTarget(
        Tournament tournament,
        CreateParticipationRequestDto dto)
    {
        if (tournament.Format == TournamentFormat.Command)
        {
            if (string.IsNullOrWhiteSpace(dto.TeamId) ||
                !string.IsNullOrWhiteSpace(dto.PlayerId))
                return BadRequest("TeamId is required");

            var team = await _teams.Find(t => t.Id == dto.TeamId)
                .FirstOrDefaultAsync();

            if (team == null)
                return NotFound("Team not found");

            if (team.SportName != tournament.SportName)
                return BadRequest("Team sport does not match");
        }
        else if (tournament.Format == TournamentFormat.Individual)
        {
            if (string.IsNullOrWhiteSpace(dto.PlayerId) ||
                !string.IsNullOrWhiteSpace(dto.TeamId))
                return BadRequest("PlayerId is required");

            var exists = await _users.Find(u =>
                u.Id == dto.PlayerId
            ).AnyAsync();

            if (!exists)
                return NotFound("Player not found");
        }
        else
        {
            return BadRequest("Unknown tournament format");
        }

        return null;
    }

    private async Task<IActionResult> CreateRequest(
        string tournamentId,
        CreateParticipationRequestDto dto,
        ParticipationRequestType type)
    {
        var actor = Actor;
        if (actor == null) return Unauthorized();

        var tournament = await _tournaments.Find(t =>
            t.Id == tournamentId
        ).FirstOrDefaultAsync();

        if (tournament == null)
            return NotFound("Tournament not found");

        if (tournament.Status != TournamentStatus.Registration)
            return Conflict("Registration is closed");

        var validation = await ValidateTarget(tournament, dto);

        if (validation != null)
            return validation;

        if (type == ParticipationRequestType.Application)
        {
            var allowed =
                tournament.Format == TournamentFormat.Command
                ? await CanManageTeam(dto.TeamId, actor)
                : dto.PlayerId == actor;

            if (!allowed)
                return Forbid();
        }
        else
        {
            if (!await CanManageTournament(tournamentId, actor))
                return Forbid();
        }

        var targetId =
            tournament.Format == TournamentFormat.Command
            ? dto.TeamId
            : dto.PlayerId;

        if (tournament.TournamentParticipants.Any(
            p => p.Id == targetId))
        {
            return Conflict("Already registered");
        }

        var existing = await _requests.Find(r =>
            r.TournamentId == tournamentId &&
            r.TeamId == dto.TeamId &&
            r.PlayerId == dto.PlayerId &&
            r.Status == ParticipationRequestStatus.Pending
        ).AnyAsync();

        if (existing)
            return Conflict("Pending request already exists");

        var request = new TournamentParticipationRequest
        {
            TournamentId = tournamentId,
            TeamId = dto.TeamId,
            PlayerId = dto.PlayerId,
            Type = type,
            CreatedByUserId = actor
        };


        try
        {
            await _requests.InsertOneAsync(request);

            return Ok(request);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError.Category ==
                  ServerErrorCategory.DuplicateKey)
        {
            return Conflict(new
            {
                message = "Pending participation request already exists"
            });
        }

    }

    [HttpPost("{tournamentId}/applications")]
    public Task<IActionResult> Apply(
        string tournamentId,
        [FromBody] CreateParticipationRequestDto dto)
    {
        return CreateRequest(
            tournamentId,
            dto,
            ParticipationRequestType.Application);
    }

    [HttpPost("{tournamentId}/invitations")]
    public Task<IActionResult> Invite(
        string tournamentId,
        [FromBody] CreateParticipationRequestDto dto)
    {
        return CreateRequest(
            tournamentId,
            dto,
            ParticipationRequestType.Invitation);
    }

    [HttpGet("{tournamentId}/requests")]
    public async Task<IActionResult> GetRequests(
        string tournamentId)
    {
        var actor = Actor;
        if (actor == null) return Unauthorized();

        if (!await CanManageTournament(tournamentId, actor))
            return Forbid();

        var items = await _requests.Find(r =>
            r.TournamentId == tournamentId
        ).SortByDescending(r => r.CreatedAt).ToListAsync();

        return Ok(items);
    }

    [HttpGet("requests/my")]
    public async Task<IActionResult> MyRequests()
    {
        var actor = Actor;
        if (actor == null) return Unauthorized();

        var memberships = await _members.Find(m =>
            m.UserId == actor &&
            m.IsActive &&
            (m.Role == TeamRole.Captain ||
             m.Role == TeamRole.Coach)
        ).ToListAsync();

        var ownedTeams = await _teams.Find(t =>
            t.OwnerId == actor
        ).ToListAsync();

        var teamIds = memberships.Select(m => m.TeamId)
            .Concat(ownedTeams.Select(t => t.Id))
            .Distinct()
            .ToList();

        var filter = Builders<TournamentParticipationRequest>
            .Filter.Or(
                Builders<TournamentParticipationRequest>
                    .Filter.Eq(r => r.PlayerId, actor),
                Builders<TournamentParticipationRequest>
                    .Filter.In(r => r.TeamId, teamIds)
            );

        var requests = await _requests.Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }


    [HttpPatch("requests/{requestId}/accept")]
    public async Task<IActionResult> Accept(
        string requestId,
        CancellationToken ct)
    {
        var actor = Actor;

        if (string.IsNullOrWhiteSpace(actor))
            return Unauthorized();

        if (!MongoDB.Bson.ObjectId.TryParse(requestId, out _))
            return BadRequest("Invalid request ID");

        try
        {
            await _approval.ApproveAsync(
                requestId,
                actor,
                async (request, userId) =>
                {
                    if (!string.IsNullOrWhiteSpace(request.PlayerId))
                    {
                        return request.PlayerId == userId;
                    }

                    if (!string.IsNullOrWhiteSpace(request.TeamId))
                    {
                        return await CanManageTeam(
                            request.TeamId, userId);
                    }

                    return false;
                },
                ct);

            return Ok(new
            {
                message = "Participation successfully approved",
                requestId
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }


    [HttpPatch("requests/{requestId}/reject")]
    public async Task<IActionResult> Reject(string requestId)
    {
        return await Resolve(
            requestId,
            ParticipationRequestStatus.Rejected);
    }

    [HttpPatch("requests/{requestId}/cancel")]
    public async Task<IActionResult> Cancel(string requestId)
    {
        return await Resolve(
            requestId,
            ParticipationRequestStatus.Cancelled);
    }

    private async Task<IActionResult> Resolve(
        string requestId,
        ParticipationRequestStatus targetStatus)
    {
        var actor = Actor;
        if (actor == null) return Unauthorized();

        var request = await _requests.Find(r =>
            r.Id == requestId &&
            r.Status == ParticipationRequestStatus.Pending
        ).FirstOrDefaultAsync();

        if (request == null)
            return NotFound("Pending request not found");

        bool allowed;

        if (targetStatus == ParticipationRequestStatus.Cancelled)
        {
            allowed =
                request.CreatedByUserId == actor;
        }
        else if (request.Type ==
                 ParticipationRequestType.Application)
        {
            allowed = await CanManageTournament(
                request.TournamentId, actor);
        }
        else
        {
            allowed = await IsTargetAuthorized(request, actor);
        }

        if (!allowed) return Forbid();

        var result = await _requests.UpdateOneAsync(
            r => r.Id == requestId &&
                 r.Status == ParticipationRequestStatus.Pending,
            Builders<TournamentParticipationRequest>.Update
                .Set(r => r.Status, targetStatus)
                .Set(r => r.ReviewedByUserId, actor)
                .Set(r => r.ReviewedAt, DateTime.UtcNow)
        );

        if (result.ModifiedCount == 0)
            return Conflict("Request already processed");

        return NoContent();
    }
}
