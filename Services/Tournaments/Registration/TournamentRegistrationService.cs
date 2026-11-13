using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Mappings;
using Beckend.Models;
using Beckend.Repositories.Teams;
using Beckend.Repositories.Tournaments;

namespace Beckend.Services.Tournaments.Registration;

public class TournamentRegistrationService: ITournamentRegistrationService
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMapper _mapper;

    public TournamentRegistrationService(
        ITournamentRepository tournamentRepository,
        ITeamRepository teamRepository,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
    }
    public async Task<bool> AddTeamToTournament(string tournamentId, string teamId)
    {

        var tournament = await _tournamentRepository
            .GetByIdAsync(tournamentId);

        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");

        EnsureParticipantsCanBeChanged(tournament);

        var team = await _teamRepository.GetByIdAsync(teamId);

        if (team == null)
            throw new KeyNotFoundException("Team not found");

        if (tournament.MaxParticipants > 0 &&
            tournament.TournamentParticipants.Count >=
                tournament.MaxParticipants)
        {
            throw new InvalidOperationException(
                $"Tournament is full. Maximum allowed: " +
                $"{tournament.MaxParticipants}");
        }

        if (team.SportName != tournament.SportName)
        {
            throw new InvalidOperationException(
                "Team sport does not match tournament sport");
        }
        var result = tournament.AddParticipant(team);

        if (result)
        {
            await _tournamentRepository.UpdateAsync(tournamentId, tournament);
        }

        return result;
    }

    public async Task<bool> RemoveTeamFromTournament(string tournamentId, string teamId)
    {

        var tournament = await _tournamentRepository
            .GetByIdAsync(tournamentId);

        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");

        EnsureParticipantsCanBeChanged(tournament);

        var team = new Team { Id = teamId };
        var result = tournament.RemoveParticipant(team);

        if (result)
        {
            await _tournamentRepository.UpdateAsync(tournamentId, tournament);
        }
        return result;
    }
    public async Task<List<TournamentDto>> GetActiveTournamentsAsync(SportName? sport = null)
    {
        var tournaments = await _tournamentRepository.GetActiveTournamentsAsync();

        if (sport.HasValue)
        {
            tournaments = tournaments.Where(t => t.SportName == sport.Value).ToList();
        }

        var tournamentDtos = _mapper.Map<List<TournamentDto>>(tournaments);
        return tournamentDtos;
    }
    public async Task<bool> RegisterPlayerAsync(string tournamentId, string userId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");

        if (tournament.Status != TournamentStatus.Registration)
        {
            throw new InvalidOperationException(
                "Registration is not open for this tournament");
        }

        if (tournament.Format != TournamentFormat.Individual)
            throw new InvalidOperationException("This tournament is for teams, not individuals");

        if (tournament.MaxParticipants > 0 && tournament.TournamentParticipants.Count >= tournament.MaxParticipants)
            throw new InvalidOperationException($"Tournament is full. Maximum {tournament.MaxParticipants} participants allowed");

        if (tournament.TournamentParticipants.Any(p => p.Id == userId))
            throw new InvalidOperationException("User already registered for this tournament");

        var player = new Team
        {
            Id = userId,
            TeamName = $"Player_{userId.Substring(0, 6)}",
            TeamDescription = "Individual player",
            SportName = tournament.SportName
        };

        tournament.TournamentParticipants.Add(player);
        await _tournamentRepository.UpdateAsync(tournamentId, tournament);

        return true;
    }


    public async Task<bool> RegisterTeamAsync(string tournamentId, string teamId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);

        if (tournament == null)
            throw new KeyNotFoundException("Tournament not found");

        if (tournament.Status != TournamentStatus.Registration)
            throw new InvalidOperationException(
                "Registration is not open for this tournament");

        if (tournament.Format != TournamentFormat.Command)
            throw new InvalidOperationException(
                "This tournament is for individuals, not teams");

        if (tournament.MaxParticipants < 0)
            throw new InvalidOperationException(
                "Tournament has an invalid participant limit");

        if (tournament.MaxParticipants > 0 &&
            tournament.TournamentParticipants.Count >= tournament.MaxParticipants)
            throw new InvalidOperationException(
                "Tournament is full");

        var team = await _teamRepository.GetByIdAsync(teamId);

        if (team == null)
            throw new KeyNotFoundException("Team not found");

        if (team.SportName != tournament.SportName)
            throw new InvalidOperationException(
                "Team sport does not match tournament sport");

        if (tournament.TournamentParticipants.Any(p => p.Id == teamId))
            throw new InvalidOperationException(
                "Team already registered for this tournament");

        tournament.TournamentParticipants.Add(team);

        await _tournamentRepository.UpdateAsync(tournamentId, tournament);

        return true;
    }
    private static void EnsureParticipantsCanBeChanged(
    Tournament tournament)
    {
        if (tournament.Status != TournamentStatus.Draft &&
            tournament.Status != TournamentStatus.Registration)
        {
            throw new InvalidOperationException(
                "Participants can only be changed during " +
                "Draft or Registration.");
        }
    }
}
