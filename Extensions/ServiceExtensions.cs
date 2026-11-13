using Beckend.Services.Auth;
using Beckend.Services.Interfaces;
using Beckend.Services.Matches;
using Beckend.Services.Matches.Generation;
using Beckend.Services.Matches.Results;
using Beckend.Services.Olympic;
using Beckend.Services.Olympic.Interfaces;
using Beckend.Services.Search;
using Beckend.Services.Sports;
using Beckend.Services.Statistics;
using Beckend.Services.Teams;
using Beckend.Services.TournamentPlanning;
using Beckend.Services.Tournaments;
using Beckend.Services.Tournaments.Lifecycle;
using Beckend.Services.Tournaments.Registration;
using Beckend.Services.TournamentStandings;
using Beckend.Services.Users;

namespace Beckend.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<ITournamentService, TournamentService>();
        services.AddScoped<
            ITournamentRegistrationService,
            TournamentRegistrationService>();
        services.AddScoped<
            ITournamentLifecycleService,
            TournamentLifecycleService>();

        services.AddScoped<ITeamService, TeamService>();

        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<
            IMatchGenerationService,
            MatchGenerationService>();
        services.AddScoped<
            IMatchResultService,
            MatchResultService>();

        services.AddScoped<IStatisticService, StatisticService>();
        services.AddScoped<ISportService, SportService>();
        services.AddScoped<ISearchService, SearchService>();

        services.AddScoped<StandingsService>();

        services.AddScoped<
            ITournamentPlanningService,
            TournamentPlanningService>();
        services.AddScoped<
            ITournamentPlanEvaluator,
            TournamentPlanEvaluator>();

        services.AddScoped<
            IOlympicBracketService,
            OlympicBracketService>();

        return services;
    }
}