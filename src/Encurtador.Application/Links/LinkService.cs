using System.Text.RegularExpressions;

namespace Encurtador.Application.Links;

// Dona das regras de negócio. Não depende de HTTP nem de EF Core.
public sealed class LinkService(
    ILinkRepository links,
    IShortCodeGenerator codes,
    ICreationLimiter limiter,
    TimeProvider timeProvider)
{
    private const int MaxGenerationAttempts = 5;
    private const string CodeInUseMessage = "Este código já está em uso.";

    private static readonly Regex CodeFormat = new("^[A-Za-z0-9]{4,20}$", RegexOptions.CultureInvariant);

    public async Task<LinkResult<CreatedLink>> CreateAsync(CreateLinkCommand command, CancellationToken ct)
    {
        if (!await limiter.TryAcquireAsync(command.Origin, ct))
        {
            return LinkResult<CreatedLink>.Failure(
                ErrorKind.RateLimited,
                "Limite de criações por hora atingido para esta origem. Tente novamente mais tarde.");
        }

        var urlError = UrlValidator.Validate(command.TargetUrl);
        if (urlError is not null)
        {
            return LinkResult<CreatedLink>.Failure(urlError);
        }

        var now = timeProvider.GetUtcNow();
        if (command.ExpiresAt is { } requestedExpiry && requestedExpiry <= now)
        {
            return LinkResult<CreatedLink>.Failure(ErrorKind.Validation, "A data de expiração deve ser futura.");
        }

        if (command.CustomCode is null)
        {
            return await CreateWithGeneratedCodeAsync(command.TargetUrl!, command.ExpiresAt, now, ct);
        }

        return await CreateWithCustomCodeAsync(command.CustomCode, command.TargetUrl!, command.ExpiresAt, now, ct);
    }

    public async Task<AccessOutcome> RegisterAccessAsync(string code, CancellationToken ct)
    {
        if (!CodeFormat.IsMatch(code))
        {
            return new NotFound();
        }

        return await links.RegisterAccessAsync(code.ToLowerInvariant(), timeProvider.GetUtcNow(), ct);
    }

    public async Task<LinkResult<LinkStats>> GetStatsAsync(string code, string? managementKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(managementKey))
        {
            return LinkResult<LinkStats>.Failure(
                ErrorKind.Validation,
                "O cabeçalho X-Management-Key é obrigatório.");
        }

        if (!ManagementKey.IsWellFormed(managementKey))
        {
            return LinkResult<LinkStats>.Failure(
                ErrorKind.Validation,
                "A chave de gerenciamento deve ter 43 caracteres base64url.");
        }

        var link = CodeFormat.IsMatch(code)
            ? await links.FindByCodeKeyAsync(code.ToLowerInvariant(), ct)
            : null;

        if (link is null)
        {
            return LinkResult<LinkStats>.Failure(ErrorKind.NotFound, "Nenhum link foi encontrado com este código.");
        }

        if (!ManagementKey.Verify(managementKey, link.ManagementKeyHash))
        {
            return LinkResult<LinkStats>.Failure(
                ErrorKind.Unauthorized,
                "A chave de gerenciamento não corresponde a este link.");
        }

        return LinkResult<LinkStats>.Success(new LinkStats(
            link.Code,
            link.TargetUrl,
            link.CreatedAtUtc,
            link.ExpiresAtUtc,
            link.ClickCount));
    }

    private async Task<LinkResult<CreatedLink>> CreateWithCustomCodeAsync(
        string code,
        string targetUrl,
        DateTimeOffset? expiresAt,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (!CodeFormat.IsMatch(code))
        {
            return LinkResult<CreatedLink>.Failure(
                ErrorKind.Validation,
                "O código personalizado deve ter de 4 a 20 letras ou números.");
        }

        if (ReservedCodes.Contains(code))
        {
            return LinkResult<CreatedLink>.Failure(
                ErrorKind.Validation,
                "Este código é reservado e não pode ser usado.");
        }

        // Consulta prévia só para mensagem clara. A garantia é o índice único (D1).
        if (await links.FindByCodeKeyAsync(code.ToLowerInvariant(), ct) is not null)
        {
            return LinkResult<CreatedLink>.Failure(ErrorKind.Validation, CodeInUseMessage);
        }

        var (link, managementKey) = NewLink(code, targetUrl, expiresAt, now);
        if (!await links.TryInsertAsync(link, ct))
        {
            return LinkResult<CreatedLink>.Failure(ErrorKind.Validation, CodeInUseMessage);
        }

        return LinkResult<CreatedLink>.Success(ToCreatedLink(link, managementKey));
    }

    private async Task<LinkResult<CreatedLink>> CreateWithGeneratedCodeAsync(
        string targetUrl,
        DateTimeOffset? expiresAt,
        DateTimeOffset now,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxGenerationAttempts; attempt++)
        {
            var (link, managementKey) = NewLink(codes.Generate(), targetUrl, expiresAt, now);
            if (await links.TryInsertAsync(link, ct))
            {
                return LinkResult<CreatedLink>.Success(ToCreatedLink(link, managementKey));
            }
        }

        throw new InvalidOperationException(
            $"Não foi possível gerar um código único após {MaxGenerationAttempts} tentativas.");
    }

    private static (Link Link, string ManagementKey) NewLink(
        string code,
        string targetUrl,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        var managementKey = ManagementKey.Generate();
        var link = new Link(code, targetUrl, now, expiresAt, ManagementKey.Hash(managementKey));
        return (link, managementKey);
    }

    private static CreatedLink ToCreatedLink(Link link, string managementKey) =>
        new(link.Code, link.TargetUrl, link.CreatedAtUtc, link.ExpiresAtUtc, managementKey);
}
