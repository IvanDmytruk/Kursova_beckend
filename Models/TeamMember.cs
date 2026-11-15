using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class TeamMember
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TeamId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public TeamRole Role { get; set; } = TeamRole.Player;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; }

    public bool IsActive { get; set; } = true;
}