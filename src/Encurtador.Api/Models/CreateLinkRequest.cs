namespace Encurtador.Api.Models;

public sealed record CreateLinkRequest(string? TargetUrl, string? CustomCode, DateTimeOffset? ExpiresAt);
