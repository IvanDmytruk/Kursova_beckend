using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Beckend.Enums;

namespace Beckend.Models
{
    public class Sport
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("SportName")]
        public SportName SportName { get; set; }
        [BsonElement("TypeSport")]
        public TypeSport Type { get; set; }

        [BsonElement("SportDescription")]
        public string SportDescription { get; set; }

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        public Sport()
        {
            Id = ObjectId.GenerateNewId().ToString();
            CreatedAt = DateTime.UtcNow;
        }

        public Sport(SportName sportName,TypeSport typesport, string sportDescription = null) : this()
        {
            SportName = sportName;
            Type = typesport;
            SportDescription = sportDescription ?? string.Empty;
        }
    }
}