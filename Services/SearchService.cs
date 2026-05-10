// Services/SearchService.cs
using Beckend.Models;
using Beckend.Repositories;
using Beckend.Enums;

namespace Beckend.Services
{
    public class SearchService
    {
        private readonly TeamRepository _teamRepository;
        private readonly TournamentRepository _tournamentRepository;
        private readonly UserRepository _userRepository;
        private readonly MatchRepository _matchRepository;

        public SearchService(
            TeamRepository teamRepository,
            TournamentRepository tournamentRepository,
            UserRepository userRepository,
            MatchRepository matchRepository)
        {
            _teamRepository = teamRepository;
            _tournamentRepository = tournamentRepository;
            _userRepository = userRepository;
            _matchRepository = matchRepository;
        }

        public async Task<object> GetSuggestionsAsync(string query, int limit = 10)
        {
            Console.WriteLine($"Searching for: {query}");  

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new { query, suggestions = new List<object>() };

            var suggestions = new List<object>();
            var searchTerm = query.ToLower();

            Console.WriteLine($"Search term: {searchTerm}");  

            // 1. Команди
            var teams = await _teamRepository.GetTeamsByNameSearchAsync(searchTerm);
            Console.WriteLine($"Found teams: {teams.Count}");  

            foreach (var team in teams)
            {
                suggestions.Add(new
                {
                    team.Id,
                    Name = team.TeamName,
                    Type = "team",
                    Subtitle = GetSportSubtitle("team"),
                    Icon = GetSportIcon("team"),
                    PopularityScore = team.PopularityScore
                });
            }

            // 2. Гравці
            var players = await _userRepository.SearchPlayersAndCoachesAsync(searchTerm);
            foreach (var player in players.Where(p => p.Role == UserRole.Player))
            {
                var popularityScore = 0;
                if (player is ExtendedUser extendedUser && extendedUser.PlayerProfile != null)
                {
                    popularityScore = extendedUser.PlayerProfile.PopularityScore;
                }

                suggestions.Add(new
                {
                    player.Id,
                    Name = $"{player.Name} {player.Surname}",
                    Type = "player",
                    Subtitle = $"Гравець, {player.Age} років",
                    Icon = "👤",
                    PopularityScore = popularityScore
                });
            }

            // 3. Турніри
            var tournaments = await _tournamentRepository.GetTournamentsByNameSearchAsync(searchTerm);
            foreach (var tournament in tournaments)
            {
                suggestions.Add(new
                {
                    tournament.Id,
                    Name = tournament.TournamentName,
                    Type = "tournament",
                    Subtitle = $"Турнір",
                    Icon = GetTournamentIcon(tournament.TournamentType),
                    PopularityScore = tournament.PopularityScore
                });
            }

            // 4. Матчі
            var matches = await _matchRepository.GetMatchesByNameSearchAsync(searchTerm);
            foreach (var match in matches)
            {
                suggestions.Add(new
                {
                    match.Id,
                    Name = match.MatchName,
                    Type = "match",
                    Subtitle = $"{match.StartTime:dd.MM.yyyy HH:mm}",
                    Icon = "⚡",
                    PopularityScore = match.PopularityScore
                });
            }

            // Сортування за популярністю (спочатку вищі)
            var sorted = suggestions
                .OrderByDescending(s => GetPopularityScore(s))
                .Take(limit)
                .ToList();

            return new { query, suggestions = sorted };
        }

        private int GetPopularityScore(object obj)
        {
            var prop = obj.GetType().GetProperty("PopularityScore");
            if (prop != null)
            {
                return (int)(prop.GetValue(obj) ?? 0);
            }
            return 0;
        }

        private string GetSportIcon(string type)
        {
            return type switch
            {
                "team" => "⚽",
                "player" => "👤",
                "tournament" => "🏆",
                "match" => "⚡",
                _ => "🏅"
            };
        }

        private string GetSportSubtitle(string type)
        {
            return type switch
            {
                "team" => "Спортивний клуб",
                "player" => "Гравець",
                "tournament" => "Турнір",
                "match" => "Матч",
                _ => ""
            };
        }

        private string GetTournamentIcon(TournamentType type)
        {
            return type switch
            {
                TournamentType.OlympicSystem => "🏅",
                TournamentType.RoundRobin => "🔄",
                TournamentType.SwissSystem => "🇨🇭",
                TournamentType.DoubleElimination => "❌",
                _ => "🏆"
            };
        }

        public async Task IncrementPopularityAsync(string id, string type)
        {
            switch (type.ToLower())
            {
                case "team":
                    var team = await _teamRepository.GetByIdAsync(id);
                    if (team != null)
                    {
                        team.PopularityScore++;
                        await _teamRepository.UpdateAsync(id, team);
                    }
                    break;

                case "player":
                    var player = await _userRepository.GetByIdAsync(id);
                    if (player != null && player is ExtendedUser extendedPlayer && extendedPlayer.PlayerProfile != null)
                    {
                        extendedPlayer.PlayerProfile.PopularityScore++;
                        await _userRepository.UpdateAsync(id, extendedPlayer);
                    }
                    break;

                case "tournament":
                    var tournament = await _tournamentRepository.GetByIdAsync(id);
                    if (tournament != null)
                    {
                        tournament.PopularityScore++;
                        await _tournamentRepository.UpdateAsync(id, tournament);
                    }
                    break;

                case "match":
                    var match = await _matchRepository.GetByIdAsync(id);
                    if (match != null)
                    {
                        match.PopularityScore++;
                        await _matchRepository.UpdateAsync(id, match);
                    }
                    break;
            }
        }
    }
}