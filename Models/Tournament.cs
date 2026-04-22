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
        public string TournamentName { get; set; }

        [BsonElement("TournamentDescription")]
        public string TournamentDescription { get; set; }

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
        private List<Team> _tournamentParticipants;

        [BsonElement("TournamentParticipants")]
        public List<Team> TournamentParticipants
        {
            get => _tournamentParticipants ?? new List<Team>();
            set => _tournamentParticipants = value;
        }

        [BsonElement("NumberOfParticipants")]
        public int NumberOfParticipants => TournamentParticipants?.Count ?? 0;

        public Tournament()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }

        public bool AddParticipant(Team team)
        {
            if (team == null)
                throw new ArgumentNullException(nameof(team));

            if (TournamentParticipants.Contains(team))
                return false;

            TournamentParticipants.Add(team);
            return true;
        }

        public bool RemoveParticipant(Team team)
        {
            return TournamentParticipants.Remove(team);
        }

        public event EventHandler<int> OnParticipantsChanged;
    }
}