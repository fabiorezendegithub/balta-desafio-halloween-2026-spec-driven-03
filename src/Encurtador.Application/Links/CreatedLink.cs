namespace Encurtador.Application.Links;

// ManagementKey é a chave em texto, retornada somente na criação.
public sealed record CreatedLink(
    string Code,
    string TargetUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string ManagementKey);
