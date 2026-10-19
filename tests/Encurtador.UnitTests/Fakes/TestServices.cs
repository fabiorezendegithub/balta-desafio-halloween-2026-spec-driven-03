using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Fakes;

public static class TestServices
{
    public static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    public static LinkService Create(
        InMemoryLinkRepository repository,
        IShortCodeGenerator? generator = null,
        ICreationLimiter? limiter = null,
        TimeProvider? time = null) =>
        new(
            repository,
            generator ?? new ShortCodeGenerator(),
            limiter ?? new FakeCreationLimiter(),
            time ?? new FixedTimeProvider(Now));
}
