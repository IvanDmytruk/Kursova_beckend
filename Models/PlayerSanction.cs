using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class PlayerSanction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TournamentId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;

    public PlayerSanctionType Type { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }
    public string? RevokedBy { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}
