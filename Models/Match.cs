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
        public DateTime? StartTime { get; set; }
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
        [BsonElement("Status")]
        public MatchStatus Status { get; set; } = MatchStatus.Planing;

        [BsonElement("RoundNumber")]
        public int RoundNumber { get; set; } = 0;

        [BsonElement("GroupId")]
        public string? GroupId { get; set; }

        [BsonElement("HomeScore")]
        public int? HomeScore { get; set; }

        [BsonElement("AwayScore")]
        public int? AwayScore { get; set; }

        [BsonElement("WinnerId")]
        public string? WinnerId { get; set; }

        [BsonElement("IsAutomatic")]
        public bool IsAutomatic { get; set; } = false;
        public Match()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
    }
}