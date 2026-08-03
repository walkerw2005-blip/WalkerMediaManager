using System.Collections.Generic;
using System.Linq;

namespace WalkerMediaManager.UI.Models;

public sealed class ShoppingPlan
{
    public decimal Budget { get; init; }
    public IReadOnlyList<ShoppingRecommendation> Recommendations { get; init; } = [];
    public decimal TotalCost => Recommendations.Sum(item => item.EstimatedPrice);
    public decimal RemainingBudget => Budget - TotalCost;
    public int CollectionsCompleted => Recommendations.Count(item => item.CompletesCollection);
    public string SummaryDisplay => Recommendations.Count == 0
        ? "No recommendations fit this budget."
        : $"{Recommendations.Count} title(s), {CollectionsCompleted} collection(s) completed, {RemainingBudget:C} remaining";
}
