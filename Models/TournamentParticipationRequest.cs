
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models
{
    public enum ParticipationRequestType
    {
        Application,
        Invitation
    }
    public enum ParticipationRequestStatus
    {
        Pending,
        Accepted,
        Rejected,
        Cancelled
    }
    public class TournamentParticipationRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } =
            ObjectId.GenerateNewId().ToString();

        public string TournamentId { get; set; } = "";
        public string TeamId { get; set; } = "";

        public ParticipationRequestType Type { get; set; }
        public ParticipationRequestStatus Status { get; set; } =
            ParticipationRequestStatus.Pending;

        public string CreatedByUserId { get; set; } = "";
        public string? ReviewedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
    }
}
