using Match = Beckend.Models.Match;
using System.ComponentModel.DataAnnotations;
using Beckend.DTOs;
using Beckend.Enums;
using Xunit;

namespace Beckend.Tests.Matches;

public class AdminMatchResultDtoTests
{
    private static List<ValidationResult> Validate(
        AdminMatchResultDto dto)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            results,
            validateAllProperties: true);

        return results;
    }

    private static AdminMatchResultDto ValidDto() => new()
    {
        ResultType = MatchResultType.TechnicalWin,
        WinnerTeamId = "team-1",
        Reason = "Opponent failed to attend",
        HomeScore = 3,
        AwayScore = 0
    };

    [Fact]
    public void ValidDto_ShouldPassValidation()
    {
        Assert.Empty(Validate(ValidDto()));
    }

    [Fact]
    public void MissingWinner_ShouldFailValidation()
    {
        var dto = ValidDto();
        dto.WinnerTeamId = "";

        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void ShortReason_ShouldFailValidation()
    {
        var dto = ValidDto();
        dto.Reason = "No";

        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void NegativeScore_ShouldFailValidation()
    {
        var dto = ValidDto();
        dto.HomeScore = -1;

        Assert.NotEmpty(Validate(dto));
    }
}
