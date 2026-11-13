using Beckend.DTOs.Olympic;
using Beckend.Models;
using Beckend.Models.Olympic;

namespace Beckend.Services.Olympic;

public static class OlympicBracketMapper
{
    public static OlympicBracketResponseDto ToResponse(
        OlympicBracketState state,
        List<Match> matches,
        List<string> automaticAdvancements)
    {
        return new OlympicBracketResponseDto
        {
            TournamentId = state.TournamentId,
            StageId = state.StageId,
            ParticipantCount = state.ParticipantCount,
            BracketSize = state.BracketSize,
            RoundNumber = state.CurrentRound,
            ByesCount = automaticAdvancements.Count,
            MatchesCount = matches.Count,
            IsCompleted = state.IsCompleted,
            ChampionTeamId = state.ChampionTeamId,
            AutomaticAdvancements = automaticAdvancements,

            Matches = matches.Select(m => new OlympicMatchDto
            {
                Id = m.Id,
                HomeTeamId = m.HomeTeamId,
                AwayTeamId = m.AwayTeamId,
                RoundNumber = m.RoundNumber,
                BracketPosition = m.BracketPosition
            }).ToList()
        };
    }
}