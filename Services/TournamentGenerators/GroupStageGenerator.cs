using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators;

public class GroupStageGenerator
{
    private readonly RoundRobinGenerator _roundRobinGenerator;

    public GroupStageGenerator(RoundRobinGenerator roundRobinGenerator)
    {
        _roundRobinGenerator = roundRobinGenerator;
    }

    public GroupStageGenerationResult Generate(Tournament tournament)
    {
        if (tournament.TournamentType != TournamentType.GroupStage)
            throw new InvalidOperationException(
                "Tournament must use GroupStage format");

        var settings = tournament.Settings
            ?? new TournamentSettings();

        var participants = tournament.TournamentParticipants.ToList();

        int groupsCount = settings.GroupsCount;
        int advancing = settings.ParticipantsAdvancing;

        if (groupsCount < 1)
            throw new InvalidOperationException(
                "GroupsCount must be at least 1");

        if (participants.Count < groupsCount * 2)
            throw new InvalidOperationException(
                "Each group must contain at least 2 teams");

        if (participants.Any(t => string.IsNullOrWhiteSpace(t.Id)) ||
            participants.Select(t => t.Id).Distinct().Count()
                != participants.Count)
        {
            throw new InvalidOperationException(
                "Invalid or duplicate participants");
        }

        if (participants.Count % groupsCount != 0)
            throw new InvalidOperationException(
                "Participants must be evenly divisible into groups");

        if (advancing < groupsCount ||
            advancing % groupsCount != 0 ||
            advancing >= participants.Count)
        {
            throw new InvalidOperationException(
                "Invalid ParticipantsAdvancing value");
        }

        int teamsPerGroup = participants.Count / groupsCount;
        int advancingPerGroup = advancing / groupsCount;

        if (advancingPerGroup >= teamsPerGroup)
            throw new InvalidOperationException(
                "Too many advancing teams per group");

        if (settings.MatchesPerPair < 1)
            throw new InvalidOperationException(
                "MatchesPerPair must be positive");

        if (settings.ShuffleParticipants)
        {
            var shuffled = participants.ToArray();
            Random.Shared.Shuffle(shuffled);
            participants = shuffled.ToList();
        }

        var result = new GroupStageGenerationResult();

        for (int groupIndex = 0; groupIndex < groupsCount; groupIndex++)
        {
            string groupId = $"group-{groupIndex + 1}";

            var groupTeams = participants
                .Skip(groupIndex * teamsPerGroup)
                .Take(teamsPerGroup)
                .ToList();

            result.Groups.Add(new TournamentGroup
            {
                TournamentId = tournament.Id,
                GroupId = groupId,
                GroupName = $"Group {(char)('A' + groupIndex)}",
                TeamIds = groupTeams.Select(t => t.Id).ToList(),
                AdvancingCount = advancingPerGroup
            });
            var groupTournament = new Tournament
            {
                Id = tournament.Id,
                TournamentType = TournamentType.RoundRobin,
                SportName = tournament.SportName,
                TournamentParticipants = groupTeams,
                Settings = new TournamentSettings
                {
                    MatchesPerPair = settings.MatchesPerPair,
                    ShuffleParticipants = false
                }
            };

            var groupMatches =
                _roundRobinGenerator.Generate(groupTournament);

            foreach (var match in groupMatches)
            {
                match.GroupId = groupId;
                match.StageId = "groups";
            }

            result.Matches.AddRange(groupMatches);
        }

        return result;
    }
}
public class GroupStageGenerationResult
{
    public List<TournamentGroup> Groups { get; set; } = new();

    public List<Match> Matches { get; set; } = new();
}