using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories;
using MongoDB.Driver;
using Beckend.Services.TournamentGenerators;

namespace Beckend.Services
{
    public class MatchService
    {
        private readonly MatchRepository _matchRepository;
        private readonly IMapper _mapper;
        private readonly TournamentRepository _tournamentRepository;
        private readonly RoundRobinGenerator _roundRobinGenerator;
        private readonly FixedRoundsSwissGenerator _fixedSwissGenerator;
        private readonly ThresholdSwissGenerator _thresholdSwissGenerator;
        private readonly IMongoClient _mongoClient;
        private readonly SwissByeRepository _swissByeRepository;
        public MatchService(
            MatchRepository matchRepository,
            TournamentRepository tournamentRepository,
            RoundRobinGenerator roundRobinGenerator,
            FixedRoundsSwissGenerator fixedSwissGenerator,
            ThresholdSwissGenerator thresholdSwissGenerator,
            IMongoClient mongoClient, SwissByeRepository swissByeRepository,
            IMapper mapper)
        {
            _matchRepository = matchRepository;
            _tournamentRepository = tournamentRepository;
            _roundRobinGenerator = roundRobinGenerator;
            _fixedSwissGenerator = fixedSwissGenerator;
            _thresholdSwissGenerator = thresholdSwissGenerator;
            _mongoClient = mongoClient;
            _mapper = mapper;
            _swissByeRepository = swissByeRepository;
        }
        public async Task<MatchDto> GetMatchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Match name is required");

            var match = await _matchRepository.GetMatchByNameAsync(name);

            if (match == null)
                throw new KeyNotFoundException($"Match with name '{name}' not found");

            return _mapper.Map<MatchDto>(match);
        }

        public async Task<List<MatchDto>> GetMatchesByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID is required");

            var matches = await _matchRepository.GetMatchesByTournamentIdAsync(tournamentId);
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<List<MatchDto>> GetMatchesByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID is required");

            var matches = await _matchRepository.GetMatchesByTeamIdAsync(teamId);
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<List<MatchDto>> GetUpcomingMatchesAsync()
        {
            var matches = await _matchRepository.GetUpcomingMatchesAsync();
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<List<MatchDto>> GetMatchesByDateRangeAsync(DateTime start, DateTime end)
        {
            if (start > end)
                throw new ArgumentException("Start date must be before end date");

            var matches = await _matchRepository.GetMatchesByDateRangeAsync(start, end);
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<bool> MatchExistsAsync(string name)
        {
            var match = await _matchRepository.GetMatchByNameAsync(name);
            return match != null;
        }

        public async Task<List<MatchDto>> GetAllAsync()
        {
            var matches = await _matchRepository.GetAllAsync();
            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<MatchDto?> GetByIdAsync(string id)
        {
            var match = await _matchRepository.GetByIdAsync(id);

            if (match == null)
                return null;

            return _mapper.Map<MatchDto>(match);
        }

        public async Task<MatchDto> CreateAsync(CreateMatchDto createDto)
        {
            var exists = await MatchExistsAsync(createDto.MatchName);
            if (exists)
                throw new InvalidOperationException($"Match with name '{createDto.MatchName}' already exists");

            if (string.IsNullOrWhiteSpace(createDto.MatchName))
                throw new ArgumentException("Match name is required");

            if (createDto.StartTime < DateTime.UtcNow)
                throw new ArgumentException("Start time cannot be in the past");

            if (createDto.TicketCost < 0)
                throw new ArgumentException("Ticket cost cannot be negative");

            if (createDto.MaxViewers <= 0)
                throw new ArgumentException("Max viewers must be greater than 0");

            if (string.IsNullOrWhiteSpace(createDto.HomeTeamId))
                throw new ArgumentException("Home team is required");

            if (string.IsNullOrWhiteSpace(createDto.AwayTeamId))
                throw new ArgumentException("Away team is required");

            if (createDto.HomeTeamId == createDto.AwayTeamId)
                throw new ArgumentException("Home team and away team cannot be the same");

            var match = _mapper.Map<Match>(createDto);
            await _matchRepository.CreateAsync(match);
            return _mapper.Map<MatchDto>(match);
        }

        public async Task<MatchDto> UpdateAsync(string id, UpdateMatchDto updateDto)
        {
            var existing = await _matchRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Match with id {id} not found");

            if (existing.IsAutomatic)
            {
                throw new InvalidOperationException(
                    "Automatically generated matches cannot be edited " +
                    "through the regular update endpoint.");
            }

            if (existing.Status == MatchStatus.Completed || existing.Status == MatchStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cannot modify a completed or cancelled match");
            }
            if (existing.MatchName != updateDto.MatchName)
            {
                var exists = await MatchExistsAsync(updateDto.MatchName);
                if (exists)
                    throw new InvalidOperationException($"Match with name '{updateDto.MatchName}' already exists");
            }

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _matchRepository.UpdateAsync(id, existing);
            return _mapper.Map<MatchDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _matchRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            if (existing.IsAutomatic)
            {
                throw new InvalidOperationException(
                    "Automatically generated matches cannot be deleted " +
                    "through the regular delete endpoint.");
            }

            await _matchRepository.DeleteAsync(id);
            return true;
        }

        public async Task<List<MatchDto>> GetMatchesStartingSoonAsync(int hours = 24)
        {
            var now = DateTime.UtcNow;
            var soon = now.AddHours(hours);

            var matches = await _matchRepository.GetMatchesByDateRangeAsync(now, soon);
            return _mapper.Map<List<MatchDto>>(matches);
        }
        public async Task<List<MatchDto>> GetUpcomingMatchesAsync(SportName? sport = null)
        {
            var matches = await _matchRepository.GetUpcomingMatchesAsync();

            if (sport.HasValue)
            {
                matches = matches.Where(m => m.SportName == sport.Value).ToList();
            }

            var matchDtos = _mapper.Map<List<MatchDto>>(matches);
            return matchDtos;
        }


        public async Task<List<MatchDto>> GenerateRoundRobinAsync(string tournamentId)
        {
            var tournament = await _tournamentRepository
                .GetByIdAsync(tournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournament.TournamentType != TournamentType.RoundRobin)
                throw new InvalidOperationException(
                    "Tournament must use Round Robin format");

            if (tournament.Status != TournamentStatus.Ready)
                throw new InvalidOperationException(
                    "Tournament must have Ready status");

            if (tournament.TournamentParticipants.Count < 2)
                throw new InvalidOperationException(
                    "At least 2 participants are required");

            var existingMatches = await _matchRepository
                .GetMatchesByTournamentIdAsync(tournamentId);

            if (existingMatches.Count > 0)
                throw new InvalidOperationException(
                    "Matches have already been created");

            var matches = _roundRobinGenerator.Generate(tournament);

            if (matches.Count == 0)
                throw new InvalidOperationException(
                    "Round Robin generator returned no matches");

            using var session = await _mongoClient.StartSessionAsync();

            session.StartTransaction();

            try
            {
                bool started = await _tournamentRepository
                    .StartTournamentAsync(
                        session,
                        tournamentId,
                        TournamentType.RoundRobin);

                if (!started)
                    throw new InvalidOperationException(
                        "Tournament is no longer Ready");

                await _matchRepository.CreateManyAsync(
                    session, matches);

                await session.CommitTransactionAsync();
            }
            catch
            {
                await session.AbortTransactionAsync();
                throw;
            }

            return _mapper.Map<List<MatchDto>>(matches);
        }

        public async Task<MatchDto> UpdateResultAsync(
            string matchId,
            UpdateMatchResultDto dto)
        {
            var match = await _matchRepository.GetByIdAsync(matchId);

            if (match == null)
                throw new KeyNotFoundException("Match not found");

            var tournament = await _tournamentRepository
                .GetByIdAsync(match.TournamentId);

            if (tournament == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournament.Status != TournamentStatus.InProgress)
                throw new InvalidOperationException(
                    "Tournament must be InProgress");

            if (match.Status == MatchStatus.Completed ||
                match.Status == MatchStatus.Cancelled)
                throw new InvalidOperationException(
                    "Cannot update a completed or cancelled match");

            if (!dto.HomeScore.HasValue || !dto.AwayScore.HasValue)
                throw new ArgumentException(
                    "Both scores are required");

            int homeScore = dto.HomeScore.Value;
            int awayScore = dto.AwayScore.Value;

            if (homeScore < 0 || awayScore < 0)
                throw new ArgumentException(
                    "Scores cannot be negative");

            bool isOlympicMatch =
                tournament.TournamentType == TournamentType.OlympicSystem &&
                match.IsAutomatic &&
                match.StageId == "main";

            bool isThresholdSwiss =
                tournament.TournamentType == TournamentType.SwissSystem &&
                tournament.Settings?.SwissMode == SwissMode.WinLossThreshold;

            if (homeScore == awayScore &&
                (isOlympicMatch ||
                 isThresholdSwiss ||
                 tournament.Settings?.AllowDraws != true))
            {
                throw new InvalidOperationException(
                    "Draws are not allowed for this match.");
            }

            match.HomeScore = homeScore;
            match.AwayScore = awayScore;

            match.WinnerId = homeScore > awayScore
                ? match.HomeTeamId
                : awayScore > homeScore
                    ? match.AwayTeamId
                    : null;

            match.Status = MatchStatus.Completed;

            using var session = await _mongoClient.StartSessionAsync();
            session.StartTransaction();

            try
            {
                bool updated = await _matchRepository
                    .CompleteMatchAsync(session, match);

                if (!updated)
                {
                    throw new InvalidOperationException(
                        "Match result has already been updated.");
                }
                if (tournament.TournamentType ==
                    TournamentType.RoundRobin)
                {
                    bool hasUnfinished = await _matchRepository
                        .HasUnfinishedMatchesAsync(
                            session,
                            tournament.Id);

                    if (!hasUnfinished)
                    {
                        bool completed = await _tournamentRepository
                            .CompleteRoundRobinAsync(
                                session,
                                tournament.Id);

                        if (!completed)
                        {
                            throw new InvalidOperationException(
                                "Could not complete Round Robin tournament.");
                        }
                    }
                }

                if (tournament.TournamentType ==
                        TournamentType.SwissSystem &&
                    tournament.Settings?.SwissMode ==
                        SwissMode.FixedRounds)
                {
                    bool hasUnfinished = await _matchRepository
                        .HasUnfinishedMatchesAsync(
                            session,
                            tournament.Id);

                    if (!hasUnfinished)
                    {
                        int finalRound =
                            tournament.Settings.RoundsCount;

                        if (finalRound > 0 &&
                            tournament.SwissLastGeneratedRound ==
                                finalRound)
                        {
                            bool completed =
                                await _tournamentRepository
                                    .CompleteFixedRoundsSwissAsync(
                                        session,
                                        tournament.Id,
                                        finalRound);

                            if (!completed)
                            {
                                throw new InvalidOperationException(
                                    "Could not complete FixedRounds Swiss tournament.");
                            }
                        }
                    }
                }
                if (isThresholdSwiss)
                {
                    bool hasUnfinished = await _matchRepository
                        .HasUnfinishedMatchesAsync(
                            session,
                            tournament.Id);

                    if (!hasUnfinished)
                    {
                        var swissMatches = await _matchRepository
                            .GetSwissMatchesAsync(
                                session,
                                tournament.Id);

                        var swissByes = await _swissByeRepository
                            .GetByTournamentAsync(
                                session,
                                tournament.Id);

                        var settings = tournament.Settings!;

                        var records =
                            tournament.TournamentParticipants
                                .ToDictionary(
                                    t => t.Id,
                                    t => (Wins: 0, Losses: 0));
                        foreach (var swissMatch in swissMatches)
                        {
                            if (!records.ContainsKey(
                                    swissMatch.HomeTeamId) ||
                                !records.ContainsKey(
                                    swissMatch.AwayTeamId) ||
                                swissMatch.HomeTeamId ==
                                    swissMatch.AwayTeamId)
                            {
                                throw new InvalidOperationException(
                                    "Swiss match contains invalid participants");
                            }

                            if (swissMatch.Status !=
                                    MatchStatus.Completed ||
                                !swissMatch.HomeScore.HasValue ||
                                !swissMatch.AwayScore.HasValue)
                            {
                                throw new InvalidOperationException(
                                    "Swiss match is not completed");
                            }

                            if (swissMatch.HomeScore.Value ==
                                swissMatch.AwayScore.Value)
                            {
                                throw new InvalidOperationException(
                                    "Threshold Swiss cannot contain draws");
                            }

                            bool homeWon =
                                swissMatch.HomeScore.Value >
                                swissMatch.AwayScore.Value;

                            string winnerId = homeWon
                                ? swissMatch.HomeTeamId
                                : swissMatch.AwayTeamId;

                            string loserId = homeWon
                                ? swissMatch.AwayTeamId
                                : swissMatch.HomeTeamId;

                            var winner = records[winnerId];
                            winner.Wins++;
                            records[winnerId] = winner;

                            var loser = records[loserId];
                            loser.Losses++;
                            records[loserId] = loser;
                        }

                        foreach (var bye in swissByes)
                        {
                            if (bye.TournamentId !=
                                    tournament.Id ||
                                !records.ContainsKey(
                                    bye.TeamId))
                            {
                                throw new InvalidOperationException(
                                    "Swiss BYE contains invalid participant");
                            }

                            if (bye.CountsAsWin)
                            {
                                var record = records[bye.TeamId];
                                record.Wins++;
                                records[bye.TeamId] = record;
                            }
                        }

                        bool allDecided = records.Values.All(r =>
                            r.Wins >= settings.WinsToAdvance ||
                            r.Losses >= settings.LossesToEliminate);

                        if (allDecided)
                        {
                            bool completed =
                                await _tournamentRepository
                                    .CompleteThresholdSwissAsync(
                                        session,
                                        tournament.Id);

                            if (!completed)
                            {
                                throw new InvalidOperationException(
                                    "Could not complete Threshold Swiss tournament.");
                            }
                        }
                    }
                }
                await session.CommitTransactionAsync();
            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync();

                throw;
            }

            return _mapper.Map<MatchDto>(match);
        }


        public async Task<List<MatchDto>> GenerateSwissRoundAsync( string tournamentId)
        {
            using var session = await _mongoClient.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var tournament = await _tournamentRepository
                    .GetSwissTournamentAsync(session, tournamentId);

                if (tournament == null)
                    throw new KeyNotFoundException("Tournament not found");

                if (tournament.TournamentType != TournamentType.SwissSystem)
                    throw new InvalidOperationException(
                        "Tournament must use SwissSystem");

                var existingMatches = await _matchRepository
                    .GetSwissMatchesAsync(session, tournamentId);

                var previousByes = await _swissByeRepository
                    .GetByTournamentAsync(session, tournamentId);

                int expectedRound = tournament.SwissLastGeneratedRound;
                bool firstRound = expectedRound == 0;

                if (firstRound && tournament.Status != TournamentStatus.Ready)
                    throw new InvalidOperationException(
                        "First Swiss round requires Ready status");

                if (!firstRound &&
                    tournament.Status != TournamentStatus.InProgress)
                    throw new InvalidOperationException(
                        "Next Swiss round requires InProgress status");

                int lastMatchRound = existingMatches.Count == 0
                    ? 0
                    : existingMatches.Max(m => m.RoundNumber);

                int lastByeRound = previousByes.Count == 0
                    ? 0
                    : previousByes.Max(b => b.RoundNumber);

                if (Math.Max(lastMatchRound, lastByeRound) != expectedRound)
                    throw new InvalidOperationException(
                        "Swiss round state is inconsistent");

                var settings = tournament.Settings
                    ?? new TournamentSettings();

                SwissRoundGenerationResult result = settings.SwissMode switch
                {
                    SwissMode.FixedRounds =>
                        _fixedSwissGenerator.Generate(
                            tournament, existingMatches, previousByes),

                    SwissMode.WinLossThreshold =>
                        _thresholdSwissGenerator.Generate(
                            tournament, existingMatches, previousByes),

                    _ => throw new NotSupportedException(
                        "Unsupported Swiss mode")
                };

                if (result.Matches.Count == 0 && result.Bye == null)
                    throw new InvalidOperationException(
                        "Swiss generator returned an empty round");

                int nextRound = expectedRound + 1;

                if (result.Matches.Any(m => m.RoundNumber != nextRound) ||
                    (result.Bye != null &&
                     result.Bye.RoundNumber != nextRound))
                    throw new InvalidOperationException(
                        "Generated Swiss round number is inconsistent");

                bool advanced = firstRound
                    ? await _tournamentRepository
                        .StartSwissTournamentAsync(session, tournamentId)
                    : await _tournamentRepository
                        .AdvanceSwissRoundAsync(
                            session, tournamentId, expectedRound);

                if (!advanced)
                    throw new InvalidOperationException(
                        "Swiss round was already generated or tournament state changed");

                if (result.Matches.Count > 0)
                {
                    await _matchRepository.CreateManyAsync(
                        session, result.Matches);
                }
                if (result.Bye != null)
                {
                    await _swissByeRepository.CreateByeAsync(
                        session, result.Bye);
                }
                if (settings.SwissMode == SwissMode.WinLossThreshold &&
                    result.Matches.Count == 0 &&
                    result.Bye != null)
                {


                    var records = tournament.TournamentParticipants
                        .ToDictionary(
                            t => t.Id,
                            t => (Wins: 0, Losses: 0));

                    foreach (var match in existingMatches)
                    {
                        if (!records.ContainsKey(match.HomeTeamId) ||
                            !records.ContainsKey(match.AwayTeamId) ||
                            match.HomeTeamId == match.AwayTeamId ||
                            match.Status != MatchStatus.Completed ||
                            !match.HomeScore.HasValue ||
                            !match.AwayScore.HasValue ||
                            match.HomeScore == match.AwayScore)
                        {
                            throw new InvalidOperationException(
                                "Invalid completed Threshold Swiss match");
                        }

                        string winnerId = match.HomeScore > match.AwayScore
                            ? match.HomeTeamId
                            : match.AwayTeamId;

                        string loserId = match.HomeScore > match.AwayScore
                            ? match.AwayTeamId
                            : match.HomeTeamId;

                        var winner = records[winnerId];
                        winner.Wins++;
                        records[winnerId] = winner;

                        var loser = records[loserId];
                        loser.Losses++;
                        records[loserId] = loser;
                    }

                    foreach (var bye in previousByes.Append(result.Bye))
                    {
                        if (bye.TournamentId != tournament.Id ||
                            !records.ContainsKey(bye.TeamId))
                        {
                            throw new InvalidOperationException(
                                "Invalid Threshold Swiss BYE");
                        }

                        if (!bye.CountsAsWin)
                            continue;

                        var record = records[bye.TeamId];
                        record.Wins++;
                        records[bye.TeamId] = record;
                    }

                    bool allDecided = records.Values.All(r =>
                        r.Wins >= settings.WinsToAdvance ||
                        r.Losses >= settings.LossesToEliminate);

                    if (allDecided)
                    {
                        bool completed = await _tournamentRepository
                            .CompleteThresholdSwissAsync(
                                session,
                                tournament.Id);

                        if (!completed)
                        {
                            throw new InvalidOperationException(
                                "Could not complete Threshold Swiss tournament");
                        }
                    }
                }
                await session.CommitTransactionAsync();

                return _mapper.Map<List<MatchDto>>(result.Matches);
            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync();

                throw;
            }
        }
    }
}