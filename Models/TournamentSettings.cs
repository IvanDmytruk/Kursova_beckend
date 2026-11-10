using Beckend.Enums;
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

        [BsonElement("AllowRematches")]
        public bool AllowRematches { get; set; } = false;

        [BsonElement("PointsForWin")]
        public decimal PointsForWin { get; set; } = 3m;

        [BsonElement("PointsForDraw")]
        public decimal PointsForDraw { get; set; } = 1m;

        [BsonElement("PointsForLoss")]
        public decimal PointsForLoss { get; set; } = 0m;

        [BsonElement("PointsForBye")]
        public decimal PointsForBye { get; set; } = 1m;

        [BsonElement("SwissMode")]
        public SwissMode SwissMode { get; set; } = SwissMode.WinLossThreshold;

        [BsonElement("ByeCountsAsWin")]
        public bool ByeCountsAsWin { get; set; } = false;
    }
}