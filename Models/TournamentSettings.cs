using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models
{
    public class TournamentSettings
    {
        [BsonElement("RoundsCount")]
        public int RoundsCount { get; set; } = 0;

        [BsonElement("GroupsCount")]
        public int GroupsCount { get; set; } = 0;

        [BsonElement("ParticipantsAdvancing")]
        public int ParticipantsAdvancing { get; set; } = 0;

        [BsonElement("MatchesPerPair")]
        public int MatchesPerPair { get; set; } = 1;

        [BsonElement("ShuffleParticipants")]
        public bool ShuffleParticipants { get; set; } = true;

        [BsonElement("AllowDraws")]
        public bool AllowDraws { get; set; } = false;

        [BsonElement("WinsToAdvance")]
        public int WinsToAdvance { get; set; } = 3;

        [BsonElement("LossesToEliminate")]
        public int LossesToEliminate { get; set; } = 3;
    }
}