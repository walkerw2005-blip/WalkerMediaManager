using System;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class CollectionHealthService
{
    public int Calculate(CollectionSeriesProgress collection)
    {
        if (collection.TotalCount == 0)
        {
            return 0;
        }

        double completion = collection.CompletionPercent * 0.65;
        double planning = collection.MissingCount == 0
            ? 25
            : (double)collection.WishlistCount / collection.MissingCount * 25;
        double affordability = collection.IsComplete
            ? 10
            : Math.Max(0, 10 - (double)collection.EstimatedCompletionCost / 20);

        return Math.Clamp((int)Math.Round(completion + planning + affordability), 0, 100);
    }
}
