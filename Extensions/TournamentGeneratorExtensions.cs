using Beckend.Services.Interfaces;
using Beckend.Services.TournamentGenerators;
using Beckend.Services.TournamentPlanning;
using Beckend.Services.TournamentStandings;


namespace Beckend.Extensions;

public static class TournamentGeneratorExtensions
{
    public static IServiceCollection AddTournamentGenerators(
        this IServiceCollection services)
    {
        services.AddScoped<RoundRobinGenerator>();

        services.AddScoped< IStandingsGenerator,RoundRobinStandingsGenerator>();

        services.AddScoped<ThresholdSwissGenerator>();
        services.AddScoped<FixedRoundsSwissGenerator>();

        services.AddScoped<ISwissStageSimulator,SwissStageSimulator>();

        services.AddScoped<OlympicGenerator>();
        services.AddScoped<Cs2SwissGenerator>();
        services.AddScoped<GroupStageGenerator>();
        services.AddScoped<RoundRobinStandingsGenerator>();
        services.AddScoped<GroupStageStandingsGenerator>();
        services.AddScoped<DoubleEliminationGenerator>();
        return services;
    }
}