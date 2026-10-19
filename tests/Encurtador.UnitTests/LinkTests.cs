using Encurtador.Application.Links;
using Xunit;

namespace Encurtador.UnitTests;

public class LinkTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expiry = Created.AddHours(1);

    [Fact]
    public void Link_without_expiry_never_expires()
    {
        var link = new Link("abc1234", "https://exemplo.com", Created, null, "hash");

        Assert.False(link.IsExpiredAt(Created.AddYears(100)));
    }

    [Fact]
    public void Link_is_active_before_expiry()
    {
        var link = new Link("abc1234", "https://exemplo.com", Created, Expiry, "hash");

        Assert.False(link.IsExpiredAt(Expiry.AddSeconds(-1)));
    }

    [Fact]
    public void Link_is_expired_exactly_at_expiry_instant()
    {
        var link = new Link("abc1234", "https://exemplo.com", Created, Expiry, "hash");

        Assert.True(link.IsExpiredAt(Expiry));
    }

    [Fact]
    public void Link_is_expired_after_expiry()
    {
        var link = new Link("abc1234", "https://exemplo.com", Created, Expiry, "hash");

        Assert.True(link.IsExpiredAt(Expiry.AddSeconds(1)));
    }

    [Fact]
    public void Expiry_with_other_offset_is_normalized_to_utc()
    {
        // 09:00 em UTC-3 é 12:00 em UTC, o mesmo instante de Created.
        var expiryInSaoPaulo = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));
        var link = new Link("abc1234", "https://exemplo.com", Created, expiryInSaoPaulo, "hash");

        var expectedUtc = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, link.ExpiresAtUtc!.Value.Offset);
        Assert.Equal(expectedUtc, link.ExpiresAtUtc.Value);
        Assert.True(link.IsExpiredAt(expectedUtc));
    }

    [Fact]
    public void TryRegisterClickAt_increments_only_active_links()
    {
        var link = new Link("abc1234", "https://exemplo.com", Created, Expiry, "hash");

        Assert.True(link.TryRegisterClickAt(Created));
        Assert.False(link.TryRegisterClickAt(Expiry));

        Assert.Equal(1, link.ClickCount);
    }
}
