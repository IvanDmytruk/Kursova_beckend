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

        public Team()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }

        public Team(string teamName, string teamDescription = null) : this()
        {
            TeamName = teamName;
            TeamDescription = teamDescription ?? string.Empty;
        }
    }
}