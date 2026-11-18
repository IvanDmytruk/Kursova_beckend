using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models;

public class Team
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    [BsonElement("TeamName")]
    public string TeamName { get; set; } = string.Empty;

    [BsonElement("TeamDescription")]
    public string TeamDescription { get; set; } = string.Empty;

    [BsonElement("PopularityScore")]
    public int PopularityScore { get; set; }

    [BsonElement("SportName")]
    public SportName SportName { get; set; }

    [BsonElement("OwnerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("IsArchived")]
    public bool IsArchived { get; set; } = false;

    [BsonElement("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public Team() { }

    public Team(string teamName, string teamDescription = "")
    {
        TeamName = teamName;
        TeamDescription = teamDescription;
    }
}
