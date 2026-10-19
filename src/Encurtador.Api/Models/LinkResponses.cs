namespace Encurtador.Api.Models;

public sealed record CreatedLinkResponse(
    string Code,
    string ShortUrl,
    string TargetUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string ManagementKey);

public sealed record LinkStatsResponse(
    string Code,
    string TargetUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    long ClickCount);
