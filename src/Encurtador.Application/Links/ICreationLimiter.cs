namespace Encurtador.Application.Links;

// Porta do limite de criação por origem. true quando a origem ainda pode criar.
public interface ICreationLimiter
{
    ValueTask<bool> TryAcquireAsync(string origin, CancellationToken ct);
}
