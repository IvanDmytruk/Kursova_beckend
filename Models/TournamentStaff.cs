using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace Beckend.Models;

public class TournamentStaff
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =ObjectId.GenerateNewId().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public TournamentMemberRole Role { get; set; } = TournamentMemberRole.Manager;
    public TournamentPermission Permissions { get; set; } = TournamentPermission.None;
    public string AssignedBy { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;
}