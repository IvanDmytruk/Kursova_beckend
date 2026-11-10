
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beckend.Models.Olympic;

public class OlympicBracketState
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    public string TournamentId { get; set; } = "";

    public string StageId { get; set; } = "main";

    public int ParticipantCount { get; set; }

    public int BracketSize { get; set; }

    public int CurrentRound { get; set; }

    public List<string?> Slots { get; set; } = new();

    public bool IsCompleted { get; set; }

    public string? ChampionTeamId { get; set; }

    public long Version { get; set; } = 0;

}
