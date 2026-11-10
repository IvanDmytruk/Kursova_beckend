
using Beckend.DTOs.TournamentPlanning;
using Beckend.Enums;
using Beckend.Models.TournamentPlanning;
using Beckend.Services.Interfaces;

namespace Beckend.Services.TournamentPlanning;

public class TournamentPlanningService : ITournamentPlanningService
{
    private readonly ISwissStageSimulator _swissSimulator;
    private readonly ITournamentPlanEvaluator _evaluator;

    public TournamentPlanningService(
        ISwissStageSimulator swissSimulator,
        ITournamentPlanEvaluator evaluator)
    {
        _swissSimulator = swissSimulator;
        _evaluator = evaluator;
    }

    public TournamentPlanningResponseDto Suggest(
        TournamentPlanningRequestDto request)
    {
        if (request.ParticipantCount < 2 ||
            request.PlayoffSize < 2 ||
            request.PlayoffSize >= request.ParticipantCount)
        {
            throw new ArgumentException(
                "ParticipantCount must exceed PlayoffSize, " +
                "and PlayoffSize must be at least 2.");
        }

        if (request.MatchesPerPair < 1 ||
            request.MaxMatches is <= 0 ||
            request.MaxRounds is <= 0)
        {
            throw new ArgumentException(
                "Match counts and specified limits must be positive.");
        }

        if (!Enum.IsDefined(request.PreferredType))
        {
            throw new ArgumentException(
                "Unsupported preferred tournament type.");
        }

        var response = new TournamentPlanningResponseDto
        {
            ParticipantCount = request.ParticipantCount,
            PlayoffSize = request.PlayoffSize
        };

        GenerateGroupPlans(request, response);
        GenerateSwissPlans(request, response);
        GenerateOlympicPlans(request, response);

        foreach (var plan in response.Plans)
            _evaluator.Evaluate(plan, request);

        response.Plans = response.Plans
            .OrderByDescending(p => p.IsValid)
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.EstimatedMatches)
            .ToList();

        var recommended = response.Plans
            .FirstOrDefault(p => p.IsValid);

        if (recommended != null)
        {
            recommended.IsRecommended = true;

            if (recommended.IsAlternative)
            {
                response.Messages.Add(
                    "The highest-scoring feasible plan uses " +
                    "an alternative tournament format.");
            }
        }
        else
        {
            response.Messages.Add(
                "No supported plan satisfies all constraints.");
        }

        if (request.PreferredType == TournamentType.DoubleElimination)
        {
            response.Messages.Add(
                "Double Elimination planning is not yet " +
                "implemented. Other formats are shown.");
        }

        response.Messages.Add(
            "Match and round estimates cover qualification " +
            "only, excluding the subsequent playoff.");

        return response;
    }

    private static void GenerateGroupPlans(
        TournamentPlanningRequestDto request,
        TournamentPlanningResponseDto response)
    {
        int n = request.ParticipantCount;
        int playoff = request.PlayoffSize;

        for (int groups = 1; groups <= n / 2; groups++)
        {
            if (n % groups != 0 ||
                playoff % groups != 0)
                continue;

            int size = n / groups;
            int advancing = playoff / groups;

            if (size < 2 ||
                advancing < 1 ||
                advancing >= size)
                continue;

            int matches =
                groups * size * (size - 1) / 2
                * request.MatchesPerPair;

            int rounds =
                (size % 2 == 0 ? size - 1 : size)
                * request.MatchesPerPair;

            var plan = new TournamentPlan
            {
                Type = TournamentType.GroupStage,
                FormatDescription = "Group Round Robin",
                ParticipantCount = n,
                PlayoffSize = playoff,
                GroupsCount = groups,
                TeamsPerGroup = size,
                AdvancingPerGroup = advancing,
                EstimatedMatches = matches,
                EstimatedRounds = rounds,
                IsValid = true
            };

            plan.Explanations.Add(
                $"{groups} groups of {size} participants.");

            plan.Explanations.Add(
                $"{advancing} participants advance " +
                "from each group.");

            plan.Explanations.Add(
                $"{request.MatchesPerPair} match(es) per pair.");

            response.Plans.Add(plan);
        }
    }

    private void GenerateSwissPlans(
        TournamentPlanningRequestDto request,
        TournamentPlanningResponseDto response)
    {
        int n = request.ParticipantCount;

        for (int wins = 1; wins <= 5; wins++)
        {
            for (int losses = 1; losses <= 5; losses++)
            {
                var simulation = _swissSimulator.Simulate(
                    n, wins, losses);

                if (!simulation.IsValid ||
                    simulation.AdvancedCount != request.PlayoffSize)
                    continue;

                var plan = new TournamentPlan
                {
                    Type = TournamentType.SwissSystem,
                    FormatDescription = "Swiss qualification",
                    ParticipantCount = n,
                    PlayoffSize = request.PlayoffSize,
                    WinsToAdvance = wins,
                    LossesToEliminate = losses,
                    EstimatedMatches = simulation.TotalMatches,
                    EstimatedRounds = simulation.TotalRounds,
                    IsValid = true
                };

                plan.Explanations.Add(
                    $"Advance after {wins} wins.");

                plan.Explanations.Add(
                    $"Eliminate after {losses} losses.");

                plan.Explanations.Add(
                    $"{simulation.AdvancedCount} qualify, " +
                    $"{simulation.EliminatedCount} are eliminated.");

                plan.Warnings.Add(
                    "Mathematical simulation only. " +
                    "Actual pairing feasibility is not guaranteed.");

                response.Plans.Add(plan);
            }
        }
    }

    private static void GenerateOlympicPlans(
        TournamentPlanningRequestDto request,
        TournamentPlanningResponseDto response)
    {
        int n = request.ParticipantCount;
        int playoff = request.PlayoffSize;

        if (!IsPowerOfTwo(playoff))
            return;

        int matches = n - playoff;

        int rounds = (int)Math.Ceiling(
            Math.Log2((double)n / playoff));

        var plan = new TournamentPlan
        {
            Type = TournamentType.OlympicSystem,
            FormatDescription = "Single-elimination qualification",
            ParticipantCount = n,
            PlayoffSize = playoff,
            EstimatedMatches = matches,
            EstimatedRounds = rounds,
            IsValid = true
        };

        plan.Explanations.Add(
            $"{n} participants are reduced to " +
            $"{playoff} through elimination matches.");

        plan.Explanations.Add(
            $"{matches} qualification matches are required.");

        if (!IsPowerOfTwo(n))
        {
            plan.Warnings.Add(
                "Byes or preliminary-round placement " +
                "are required for a non-power-of-two field.");
        }

        response.Plans.Add(plan);
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}
