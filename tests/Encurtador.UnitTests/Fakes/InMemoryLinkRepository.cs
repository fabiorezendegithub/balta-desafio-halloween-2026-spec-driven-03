using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Fakes;

// Implementação em memória de ILinkRepository. Respeita unicidade de CodeKey e a regra de expiração de RegisterAccessAsync.
public sealed class InMemoryLinkRepository : ILinkRepository
{
    private readonly Dictionary<string, Link> _byCodeKey = new();

    public IReadOnlyCollection<Link> All => _byCodeKey.Values;

    public int InsertAttempts { get; private set; }

    public Task<bool> TryInsertAsync(Link link, CancellationToken ct)
    {
        InsertAttempts++;

        if (_byCodeKey.ContainsKey(link.CodeKey))
        {
            return Task.FromResult(false);
        }

        _byCodeKey[link.CodeKey] = link;
        return Task.FromResult(true);
    }

    public Task<Link?> FindByCodeKeyAsync(string codeKey, CancellationToken ct)
    {
        _byCodeKey.TryGetValue(codeKey, out var link);
        return Task.FromResult(link);
    }

    public Task<AccessOutcome> RegisterAccessAsync(string codeKey, DateTimeOffset nowUtc, CancellationToken ct)
    {
        if (!_byCodeKey.TryGetValue(codeKey, out var link))
        {
            return Task.FromResult<AccessOutcome>(new NotFound());
        }

        if (!link.TryRegisterClickAt(nowUtc))
        {
            return Task.FromResult<AccessOutcome>(new Expired());
        }

        return Task.FromResult<AccessOutcome>(new Redirected(link.TargetUrl));
    }
}
