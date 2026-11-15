
using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Mappings;
using Beckend.Models;
using Xunit;

namespace Beckend.Tests.Matches;

public class MatchMappingTests
{
    private readonly IMapper _mapper;

    public MatchMappingTests()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            Microsoft.Extensions.Logging.Abstractions
                .NullLoggerFactory.Instance);

        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void UpdateMatch_ShouldNotChangeProtectedFields()
    {
        var originalDate = DateTime.UtcNow.AddDays(3);

        var match = new Match
        {
            Id = "original-id",
            TournamentId = "tournament-1",
            MatchName = "Old Match",
            HomeTeamId = "team-1",
            AwayTeamId = "team-2",
            StartTime = originalDate,
            Status = MatchStatus.Process
        };

        var dto = new UpdateMatchDto
        {
            MatchName = "Updated Match",
            TournamentId = "different-tournament",
            HomeTeamId = "team-1",
            AwayTeamId = "team-2",
            TicketCost = 100,
            MaxViewers = 500,
            StartTime = null,
            Status = "Completed",
            SportName = SportName.Football.ToString()
        };

        _mapper.Map(dto, match);

        Assert.Equal("original-id", match.Id);
        Assert.Equal("tournament-1", match.TournamentId);
        Assert.Equal(MatchStatus.Process, match.Status);
        Assert.Equal(originalDate, match.StartTime);
        Assert.Equal("Updated Match", match.MatchName);
    }
}
