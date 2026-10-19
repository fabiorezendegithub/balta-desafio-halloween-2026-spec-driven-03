using System.Threading.RateLimiting;
using Encurtador.Application.Links;
using LinkOptions = Encurtador.Application.Links.LinkOptions;

namespace Encurtador.Api.RateLimiting;

// Adaptador de ICreationLimiter. Limite por origem em janela fixa de 1 hora, em memória (instância única).
public sealed class FixedWindowCreationLimiter : ICreationLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public FixedWindowCreationLimiter(LinkOptions options)
    {
        _limiter = PartitionedRateLimiter.Create<string, string>(origin =>
            RateLimitPartition.GetFixedWindowLimiter(origin, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.CreationLimitPerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    public async ValueTask<bool> TryAcquireAsync(string origin, CancellationToken ct)
    {
        using var lease = await _limiter.AcquireAsync(origin, 1, ct);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
