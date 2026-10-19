namespace Encurtador.Application.Links;

public interface ILinkRepository
{
    // Retorna false quando já existe um link com o mesmo CodeKey.
    Task<bool> TryInsertAsync(Link link, CancellationToken ct);

    Task<Link?> FindByCodeKeyAsync(string codeKey, CancellationToken ct);

    // Incremento atômico. Só conta se o link estiver ativo em nowUtc.
    Task<AccessOutcome> RegisterAccessAsync(string codeKey, DateTimeOffset nowUtc, CancellationToken ct);
}
