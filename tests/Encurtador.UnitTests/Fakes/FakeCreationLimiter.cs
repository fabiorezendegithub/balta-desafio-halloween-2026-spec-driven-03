using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Fakes;

public sealed class FakeCreationLimiter(bool allow = true) : ICreationLimiter
{
    public bool Allow { get; set; } = allow;

    public ValueTask<bool> TryAcquireAsync(string origin, CancellationToken ct) => ValueTask.FromResult(Allow);
}
