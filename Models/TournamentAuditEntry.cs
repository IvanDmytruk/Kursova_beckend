using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class TournamentAuditEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TournamentId { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
