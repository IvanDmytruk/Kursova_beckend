using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Services.TournamentGenerators;

namespace Beckend.Services.Matches;

public class MatchService : IMatchService
{
    private readonly IMatchRepository _matchRepository;
    private readonly IMapper _mapper;

    public MatchService(
        IMatchRepository matchRepository,
        IMapper mapper)
    {
        _matchRepository = matchRepository;
        _mapper = mapper;
    }

    public async Task<MatchDto> GetMatchByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Match name is required");

        var match = await _matchRepository.GetMatchByNameAsync(name);

        if (match == null)
            throw new KeyNotFoundException(
                $"Match with name '{name}' not found");

        return _mapper.Map<MatchDto>(match);
    }

    public async Task<List<MatchDto>> GetMatchesByTournamentIdAsync(
        string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId))
            throw new ArgumentException("Tournament ID is required");

        var matches = await _matchRepository
            .GetMatchesByTournamentIdAsync(tournamentId);

        return _mapper.Map<List<MatchDto>>(matches);
    }

    public async Task<List<MatchDto>> GetMatchesByTeamIdAsync(string teamId)
    {
        if (string.IsNullOrWhiteSpace(teamId))
            throw new ArgumentException("Team ID is required");

        var matches = await _matchRepository
            .GetMatchesByTeamIdAsync(teamId);

        return _mapper.Map<List<MatchDto>>(matches);
    }

    public async Task<List<MatchDto>> GetUpcomingMatchesAsync(
        SportName? sport = null)
    {
        var matches = await _matchRepository.GetUpcomingMatchesAsync();

        if (sport.HasValue)
        {
            matches = matches
                .Where(m => m.SportName == sport.Value)
                .ToList();
        }

        return _mapper.Map<List<MatchDto>>(matches);
    }
    public async Task<List<MatchDto>> GetMatchesStartingSoonAsync(
        int hours = 24)
    {
        if (hours <= 0)
            throw new ArgumentException(
                "Hours must be greater than zero");

        var now = DateTime.UtcNow;
        var soon = now.AddHours(hours);

        var matches = await _matchRepository
            .GetMatchesByDateRangeAsync(now, soon);

        return _mapper.Map<List<MatchDto>>(matches);
    }
    public async Task<List<MatchDto>> GetMatchesByDateRangeAsync(DateTime start, DateTime end)
    {
        if (start > end)
            throw new ArgumentException(
                "Start date must be before end date");

        var matches = await _matchRepository
            .GetMatchesByDateRangeAsync(start, end);

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
        if (string.IsNullOrWhiteSpace(createDto.MatchName))
            throw new ArgumentException("Match name is required");

        var exists = await MatchExistsAsync(createDto.MatchName);

        if (exists)
            throw new InvalidOperationException(
                $"Match with name '{createDto.MatchName}' already exists");
        if (createDto == null)
            throw new ArgumentNullException(nameof(createDto));

        if (string.IsNullOrWhiteSpace(createDto.TournamentId))
            throw new ArgumentException("Tournament ID is required");

        if (string.IsNullOrWhiteSpace(createDto.SportName) ||
            !Enum.TryParse<SportName>(
                createDto.SportName,
                true,
                out var sport) ||
            !Enum.IsDefined(sport))
        {
            throw new ArgumentException("Invalid sport name");
        }
        if (createDto.StartTime < DateTime.UtcNow)
            throw new ArgumentException(
                "Start time cannot be in the past");

        if (createDto.TicketCost < 0)
            throw new ArgumentException(
                "Ticket cost cannot be negative");

        if (createDto.MaxViewers <= 0)
            throw new ArgumentException(
                "Max viewers must be greater than 0");

        if (string.IsNullOrWhiteSpace(createDto.HomeTeamId))
            throw new ArgumentException("Home team is required");

        if (string.IsNullOrWhiteSpace(createDto.AwayTeamId))
            throw new ArgumentException("Away team is required");

        if (createDto.HomeTeamId == createDto.AwayTeamId)
            throw new ArgumentException(
                "Home team and away team cannot be the same");

        var match = _mapper.Map<Match>(createDto);

        await _matchRepository.CreateAsync(match);

        return _mapper.Map<MatchDto>(match);
    }

    public async Task<MatchDto> UpdateAsync( string id, UpdateMatchDto updateDto)
    {
        var existing = await _matchRepository.GetByIdAsync(id);

        if (existing == null)
            throw new KeyNotFoundException(
                $"Match with id {id} not found");

        if (existing.IsAutomatic)
        {
            throw new InvalidOperationException(
                "Automatically generated matches cannot be edited " +
                "through the regular update endpoint.");
        }

        if (existing.Status == MatchStatus.Completed ||
            existing.Status == MatchStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cannot modify a completed or cancelled match");
        }
        if (updateDto == null)
            throw new ArgumentNullException(nameof(updateDto));

        if (string.IsNullOrWhiteSpace(updateDto.MatchName))
            throw new ArgumentException("Match name is required");

        if (updateDto.TicketCost < 0)
            throw new ArgumentException("Ticket cost cannot be negative");

        if (updateDto.MaxViewers <= 0)
            throw new ArgumentException("Max viewers must be greater than 0");

        if (string.IsNullOrWhiteSpace(updateDto.HomeTeamId) ||
            string.IsNullOrWhiteSpace(updateDto.AwayTeamId))
        {
            throw new ArgumentException("Both teams are required");
        }

        if (updateDto.HomeTeamId == updateDto.AwayTeamId)
            throw new ArgumentException("Teams cannot be the same");

        if (updateDto.StartTime.HasValue &&
            updateDto.StartTime.Value < DateTime.UtcNow)
        {
            throw new ArgumentException("Start time cannot be in the past");
        }

        if (updateDto.TournamentId != existing.TournamentId)
            throw new ArgumentException(
                "Changing a match tournament is not allowed");
        if (existing.MatchName != updateDto.MatchName)
        {
            var exists = await MatchExistsAsync(updateDto.MatchName);

            if (exists)
                throw new InvalidOperationException(
                    $"Match with name '{updateDto.MatchName}' already exists");
        }

        _mapper.Map(updateDto, existing);

        existing.Id = id;

        var updated = await _matchRepository.UpdateEditableMatchAsync(existing);

        if (!updated)
        {
            throw new InvalidOperationException(
                "Match was changed, completed, cancelled or deleted.");
        }

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
        if (existing.Status == MatchStatus.Completed || existing.Status == MatchStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cannot delete a completed or cancelled match");
        }
        var deleted = await _matchRepository.DeleteEditableMatchAsync(id);

        if (!deleted)
        {
            throw new InvalidOperationException(
                "Match was changed, completed, cancelled or deleted.");
        }

        return true;
    }
}