using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models
{
    public class Match
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } 

        [BsonElement("MatchName")]
        public string MatchName { get; set; }

        [BsonElement("TicketCost")] 
        public double TicketCost { get; set; }

        [BsonElement("MaxViewers")]
        public int MaxViewers { get; set; }

        [BsonElement("StartTime")]
        public DateTime StartTime { get; set; }

        [BsonElement("HomeTeamId")]
        public string HomeTeamId { get; set; }

        [BsonElement("AwayTeamId")]
        public string AwayTeamId { get; set; }

        [BsonElement("TournamentId")]
        public string TournamentId { get; set; }
        [BsonElement("PopularityScore")]
        public int PopularityScore { get; set; } = 0;
        [BsonElement("SportName")]
        public SportName SportName { get; set; }

        public Match()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
    }
}