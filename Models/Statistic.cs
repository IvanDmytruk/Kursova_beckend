using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models
{
    public class Statistic
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("UserId")]
        public string UserId { get; set; }

        [BsonElement("TeamId")]
        public string TeamId { get; set; }

        [BsonElement("TournamentId")]
        public string TournamentId { get; set; }

        [BsonElement("Wins")]
        public int Wins { get; set; }

        [BsonElement("Losses")] 
        public int Losses { get; set; }

        [BsonElement("Draws")] 
        public int Draws { get; set; }

        [BsonElement("Points")]
        public int Points { get; set; }

        [BsonElement("MatchesPlayed")]
        public int MatchesPlayed { get; set; }

        [BsonElement("Season")]
        public string Season { get; set; } 

        public Statistic()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
    }
}