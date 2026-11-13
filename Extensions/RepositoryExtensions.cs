using Beckend.Repositories.Matches;
using Beckend.Repositories.Olympic;
using Beckend.Repositories.Sports;
using Beckend.Repositories.Statistics;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Teams;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.Users;
using Beckend.Repositories.TournamentGroups;

namespace Beckend.Extensions;

public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IStatisticRepository, StatisticRepository>();
        services.AddScoped<ISportRepository, SportRepository>();
        services.AddScoped<ISwissByeRepository, SwissByeRepository>();
        services.AddScoped<IOlympicBracketRepository, OlympicBracketRepository>();
        services.AddScoped<ITournamentGroupRepository, TournamentGroupRepository>();

        return services;
    }
}