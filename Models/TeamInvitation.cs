using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public enum TeamInvitationStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled
}

public class TeamInvitation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TeamId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public TeamRole Role { get; set; } = TeamRole.Player;

    public TeamInvitationStatus Status { get; set; } =
        TeamInvitationStatus.Pending;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
