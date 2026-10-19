namespace Encurtador.Application.Links;

public sealed record LinkStats(
    string Code,
    string TargetUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    long ClickCount);
