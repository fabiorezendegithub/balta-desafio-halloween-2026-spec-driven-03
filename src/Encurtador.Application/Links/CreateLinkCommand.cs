namespace Encurtador.Application.Links;

public sealed record CreateLinkCommand(
    string Origin,
    string? TargetUrl,
    string? CustomCode,
    DateTimeOffset? ExpiresAt);
