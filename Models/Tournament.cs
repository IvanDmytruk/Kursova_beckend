using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Beckend.Enums;
namespace Beckend.Models
{
    public class Tournament
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } 
        [BsonElement("TournamentName")]
        public string TournamentName { get; set; } = string.Empty;
        [BsonElement("TournamentDescription")]
        public string TournamentDescription { get; set; } = string.Empty;
        [BsonElement("TournamentType")]
        public TournamentType TournamentType { get; set; }
        [BsonElement("Format")]
        public TournamentFormat Format { get; set; }
        [BsonElement("PrizeFund")]
        public int PrizeFund { get; set; }
        [BsonElement("StartDate")]
        public DateTime StartDate { get; set; }
        [BsonElement("EndDate")]
        public DateTime? EndDate { get; set; }
        [BsonElement("TournamentParticipants")]
        public List<Team> TournamentParticipants { get; set; } = new List<Team>();
        [BsonElement("NumberOfParticipants")]
        public int NumberOfParticipants => TournamentParticipants?.Count ?? 0;
        [BsonElement("PopularityScore")]
        public int PopularityScore { get; set; } = 0;
        [BsonElement("SportName")]
        public SportName SportName { get; set; }
        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty;
        [BsonElement("MaxParticipants")]
        public int MaxParticipants { get; set; } = 0;
        [BsonElement("Settings")]
        public TournamentSettings Settings { get; set; } = new();
        [BsonElement("Status")]
        public TournamentStatus Status { get; set; } =
    TournamentStatus.Draft;
        public Tournament()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
        public bool AddParticipant(Team team)
        {
            if (team == null)
                throw new ArgumentNullException(nameof(team));

            if (TournamentParticipants.Any(t => t.Id == team.Id))
                return false;

            TournamentParticipants.Add(team);
            return true;
        }

        public bool RemoveParticipant(Team team)
        {
            if (team == null)
                return false;

            return TournamentParticipants.RemoveAll(
                t => t.Id == team.Id) > 0;
        }
        public event EventHandler<int>? OnParticipantsChanged;
    }
}