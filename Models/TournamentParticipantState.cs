using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class TournamentParticipantState
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TournamentId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;

    public TournamentParticipantStatus Status { get; set; } =
        TournamentParticipantStatus.Active;

    public string? ReplacementTeamId { get; set; }
    public string? Reason { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
