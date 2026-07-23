using System;

namespace WalkerMediaManager.UI.Models;

public sealed class MediaCollection
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int TargetCount { get; set; }

    public int OwnedCount { get; set; }

    public int MissingCount =>
        TargetCount > OwnedCount
            ? TargetCount - OwnedCount
            : 0;

    public double CompletionPercent =>
        TargetCount <= 0
            ? 0
            : Math.Min(
                100,
                (double)OwnedCount / TargetCount * 100);

    public string ProgressDisplay =>
        $"{OwnedCount} of {TargetCount} owned";

    public string MissingDisplay =>
        MissingCount == 1
            ? "1 title missing"
            : $"{MissingCount} titles missing";

    public string CompletionDisplay =>
        $"{CompletionPercent:0}% complete";
}