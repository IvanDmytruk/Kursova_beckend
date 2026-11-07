
using Beckend.Enums;
using Beckend.Models;

namespace Beckend.Services.TournamentGenerators
{
    public class RoundRobinGenerator
    {
        public List<Match> Generate(Tournament tournament)
        {
            if (tournament.TournamentType != TournamentType.RoundRobin)
                throw new InvalidOperationException(
                    "Tournament is not Round Robin");

            var participants = tournament.TournamentParticipants.ToList();

            if (participants.Count < 2)
                throw new InvalidOperationException(
                    "At least 2 participants are required");

            if (participants.Select(p => p.Id).Distinct().Count()
                != participants.Count)
                throw new InvalidOperationException(
                    "Tournament contains duplicate participants");

            var matchesPerPair = tournament.Settings?.MatchesPerPair ?? 1;

            if (matchesPerPair < 1)
                throw new InvalidOperationException(
                    "MatchesPerPair must be greater than zero");

            if (tournament.Settings?.ShuffleParticipants == true)
            {
                Random.Shared.Shuffle(
                    System.Runtime.InteropServices.CollectionsMarshal
                        .AsSpan(participants));
            }

            var rotation = participants
                .Select(p => (Team?)p)
                .ToList();

            if (rotation.Count % 2 != 0)
                rotation.Add(null);

            int count = rotation.Count;
            int roundsPerCycle = count - 1;

            var matches = new List<Match>();

            for (int cycle = 0; cycle < matchesPerPair; cycle++)
            {
                for (int round = 0; round < roundsPerCycle; round++)
                {
                    int roundNumber =
                        cycle * roundsPerCycle + round + 1;

                    for (int i = 0; i < count / 2; i++)
                    {
                        var first = rotation[i];
                        var second = rotation[count - 1 - i];

                        if (first == null || second == null)
                            continue;

                        bool reverse = (round + cycle) % 2 != 0;

                        var home = reverse ? second : first;
                        var away = reverse ? first : second;

                        matches.Add(new Match
                        {
                            TournamentId = tournament.Id,
                            MatchName =
                                $"{home.TeamName} vs {away.TeamName}",
                            HomeTeamId = home.Id,
                            AwayTeamId = away.Id,
                            SportName = tournament.SportName,
                            RoundNumber = roundNumber,
                            StartTime = null,
                            Status = MatchStatus.Planing,
                            IsAutomatic = true,
                            TicketCost = 0,
                            MaxViewers = 0
                        });
                    }

                    var last = rotation[^1];
                    rotation.RemoveAt(rotation.Count - 1);
                    rotation.Insert(1, last);
                }
            }

            return matches;
        }
    }
}
