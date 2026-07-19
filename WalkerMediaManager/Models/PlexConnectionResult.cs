namespace WalkerMediaManager.Models;

public sealed record PlexConnectionResult(bool Success, string Message, string? ServerName = null);
