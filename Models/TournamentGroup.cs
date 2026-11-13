using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class TournamentGroup
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TournamentId { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public List<string> TeamIds { get; set; } = new();

    public int AdvancingCount { get; set; }
}