using Beckend.DTOs.TournamentPlanning;
using Beckend.Enums;
using Beckend.Models.TournamentPlanning;
using Beckend.Services.Interfaces;

namespace Beckend.Services.TournamentPlanning;

public class TournamentPlanEvaluator : ITournamentPlanEvaluator
{
    public void Evaluate(
        TournamentPlan plan,
        TournamentPlanningRequestDto request)
    {
        plan.IsAlternative = plan.Type != request.PreferredType;
        plan.IsRecommended = false;

        bool withinMatches =
            !request.MaxMatches.HasValue ||
            plan.EstimatedMatches <= request.MaxMatches.Value;

        bool withinRounds =
            !request.MaxRounds.HasValue ||
            plan.EstimatedRounds <= request.MaxRounds.Value;

        plan.IsValid = plan.IsValid &&
                       withinMatches &&
                       withinRounds;

        if (!withinMatches)
        {
            plan.Warnings.Add(
                $"Match limit exceeded: {plan.EstimatedMatches} " +
                $"required, {request.MaxMatches} allowed.");
        }

        if (!withinRounds)
        {
            plan.Warnings.Add(
                $"Round limit exceeded: {plan.EstimatedRounds} " +
                $"required, {request.MaxRounds} allowed.");
        }

        double score = 60;

        if (!plan.IsAlternative)
        {
            score += 15;
        }
        else
        {
            plan.Explanations.Add(
                "Alternative to the preferred tournament format.");
        }

        switch (plan.Type)
        {
            case TournamentType.SwissSystem:
            case TournamentType.Cs2SwissSystem:
                if (plan.WinsToAdvance == 1 ||
                    plan.LossesToEliminate == 1)
                {
                    score -= 35;
                    plan.Warnings.Add(
                        "One-match qualification or elimination " +
                        "provides limited competitive evaluation.");
                }
                else if (plan.WinsToAdvance >= 3 &&
                         plan.LossesToEliminate >= 3)
                {
                    score += 20;
                }
                else
                {
                    score += 10;
                }
                break;

            case TournamentType.GroupStage:
            case TournamentType.RoundRobin:
                if (plan.TeamsPerGroup == 2)
                {
                    score -= 25;
                    plan.Warnings.Add(
                        "Two-team groups offer limited comparison.");
                }
                else if (plan.TeamsPerGroup >= 4 &&
                         plan.TeamsPerGroup <= 6)
                {
                    score += 20;
                }
                else if (plan.TeamsPerGroup == 3)
                {
                    score += 5;
                }
                else
                {
                    score -= 5;
                }
                break;

            case TournamentType.OlympicSystem:
                score += 5;
                plan.Explanations.Add(
                    "Fast qualification, but one loss " +
                    "normally ends participation.");
                break;
        }

        if (request.MaxMatches.HasValue &&
            request.MaxMatches.Value > 0)
        {
            double usage =
                (double)plan.EstimatedMatches /
                request.MaxMatches.Value;

            if (usage <= 0.75)
                score += 5;
        }

        if (request.MaxRounds.HasValue &&
            request.MaxRounds.Value > 0)
        {
            double usage =
                (double)plan.EstimatedRounds /
                request.MaxRounds.Value;

            if (usage <= 0.75)
                score += 5;
        }

        if (!plan.IsValid)
        {
            plan.Score = 0;
            return;
        }

        plan.Score = Math.Clamp(score, 0, 100);
    }
}
