using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace Beckend.Models
{
    public class Team
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } 
        [BsonElement("TeamName")]
        public string TeamName { get; set; }
        [BsonElement("TeamDescription")]
        public string TeamDescription { get; set; } = string.Empty;
        [BsonElement("PopularityScore")]
        public int PopularityScore { get; set; } = 0;
        [BsonElement("SportName")]
        public SportName SportName { get; set; }

        public Team()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
        public Team(string teamName, string teamDescription = "") : this()
        {
            TeamName = teamName;
            TeamDescription = teamDescription ?? string.Empty;
        }
    }
}