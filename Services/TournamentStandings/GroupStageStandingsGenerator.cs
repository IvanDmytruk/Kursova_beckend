using Beckend.DTOs.Standings;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.TournamentGroups;
using Beckend.Repositories.TournamentAdministration;

namespace Beckend.Services.TournamentStandings;

public class GroupStageStandingsGenerator
{
    private readonly ITournamentGroupRepository _groupRepository;
    private readonly RoundRobinStandingsGenerator _roundRobin;
    private readonly ITournamentAdministrationRepository _administration;

    public GroupStageStandingsGenerator(
        ITournamentGroupRepository groupRepository,
        RoundRobinStandingsGenerator roundRobin,
        ITournamentAdministrationRepository administration)
    {
        _groupRepository = groupRepository;
        _roundRobin = roundRobin;
        _administration = administration;
    }

    public async Task<TournamentStandingsDto> GenerateAsync(
        Tournament tournament,
        List<Match> matches)
    {
        var groups = await _groupRepository
            .GetByTournamentIdAsync(tournament.Id);

        var participantStates = await _administration
            .GetParticipantStatesAsync(tournament.Id);

        return Generate(
            tournament,
            matches,
            groups,
            participantStates);
    }

    public TournamentStandingsDto Generate(
        Tournament tournament,
        List<Match> matches,
        List<TournamentGroup> groups,
        List<TournamentParticipantState> participantStates)
    {
        if (tournament.TournamentType != TournamentType.GroupStage)
            throw new InvalidOperationException(
                "Tournament is not GroupStage");

        if (groups.Count == 0)
            throw new InvalidOperationException(
                "Tournament groups have not been generated");

        var allTeamIds = groups
            .SelectMany(g => g.TeamIds)
            .ToList();

        if (allTeamIds.Count != allTeamIds.Distinct().Count())
            throw new InvalidOperationException(
                "A participant belongs to multiple groups");

        var tournamentTeams = tournament.TournamentParticipants
            .ToDictionary(t => t.Id);

        if (allTeamIds.Count != tournamentTeams.Count ||
            allTeamIds.Any(id => !tournamentTeams.ContainsKey(id)))
        {
            throw new InvalidOperationException(
                "Group membership is inconsistent");
        }

        var groupIds = groups
            .Select(g => g.GroupId)
            .ToHashSet();

        var disqualifiedIds = participantStates
            .Where(s =>
                s.Status == TournamentParticipantStatus.Disqualified)
            .Select(s => s.TeamId)
            .ToHashSet();

        var groupMatches = matches
            .Where(m => m.StageId == "groups")
            .ToList();

        if (groupMatches.Any(m =>
            m.GroupId == null ||
            !groupIds.Contains(m.GroupId)))
        {
            throw new InvalidOperationException(
                "Match references an unknown group");
        }

        var sections = new List<StandingsSectionDto>();

        foreach (var group in groups)
        {
            var teamIds = group.TeamIds.ToHashSet();

            var teams = group.TeamIds
                .Select(id => tournamentTeams[id])
                .ToList();

            var matchesInGroup = groupMatches
                .Where(m => m.GroupId == group.GroupId)
                .ToList();

            if (matchesInGroup.Any(m =>
                !teamIds.Contains(m.HomeTeamId) ||
                !teamIds.Contains(m.AwayTeamId)))
            {
                throw new InvalidOperationException(
                    "Match contains a team outside its group");
            }

            var groupTournament = new Tournament
            {
                Id = tournament.Id,
                TournamentType = TournamentType.RoundRobin,
                TournamentParticipants = teams,
                Settings = tournament.Settings,
                SportName = tournament.SportName
            };

            var standings = _roundRobin.Generate(
                groupTournament,
                matchesInGroup);

            var entries = standings.Sections.Single().Entries;

            foreach (var entry in entries)
            {
                entry.IsDisqualified =
                    disqualifiedIds.Contains(entry.ParticipantId);

                entry.Status = entry.IsDisqualified
                    ? "Disqualified"
                    : "Active";
            }

            sections.Add(new StandingsSectionDto
            {
                SectionId = group.GroupId,
                SectionName = group.GroupName,
                Entries = entries
            });
        }

        return new TournamentStandingsDto
        {
            TournamentId = tournament.Id,
            TournamentType = tournament.TournamentType.ToString(),
            ViewType = "Groups",
            Sections = sections
        };
    }

}