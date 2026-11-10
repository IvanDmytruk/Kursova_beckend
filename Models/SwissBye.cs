using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class SwissBye
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    [BsonElement("TournamentId")]
    public string TournamentId { get; set; } = string.Empty;

    [BsonElement("TeamId")]
    public string TeamId { get; set; } = string.Empty;

    [BsonElement("RoundNumber")]
    public int RoundNumber { get; set; }

    [BsonElement("PointsAwarded")]
    public decimal PointsAwarded { get; set; }

    [BsonElement("CountsAsWin")]
    public bool CountsAsWin { get; set; }
}
