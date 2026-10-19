using Encurtador.Application.Links;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Infrastructure.Persistence;

public sealed class EfLinkRepository(AppDbContext db) : ILinkRepository
{
    public async Task<bool> TryInsertAsync(Link link, CancellationToken ct)
    {
        db.Links.Add(link);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (UniqueConstraintDetector.IsUniqueViolation(ex))
        {
            // Remove a entidade rejeitada do contexto, para que uma nova tentativa não a reinsira.
            db.Entry(link).State = EntityState.Detached;
            return false;
        }
    }

    public Task<Link?> FindByCodeKeyAsync(string codeKey, CancellationToken ct) =>
        db.Links.AsNoTracking().FirstOrDefaultAsync(l => l.CodeKey == codeKey, ct);

    public async Task<AccessOutcome> RegisterAccessAsync(string codeKey, DateTimeOffset nowUtc, CancellationToken ct)
    {
        // D2: incremento atômico em uma única instrução, sem ler e reescrever o contador.
        var updated = await db.Links
            .Where(l => l.CodeKey == codeKey && (l.ExpiresAtUtc == null || l.ExpiresAtUtc > nowUtc))
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.ClickCount, l => l.ClickCount + 1),
                ct);

        if (updated == 1)
        {
            var targetUrl = await db.Links
                .Where(l => l.CodeKey == codeKey)
                .Select(l => l.TargetUrl)
                .FirstAsync(ct);

            return new Redirected(targetUrl);
        }

        // Nenhuma linha afetada: ou não existe, ou está expirado.
        var exists = await db.Links.AnyAsync(l => l.CodeKey == codeKey, ct);
        return exists ? new Expired() : new NotFound();
    }
}
