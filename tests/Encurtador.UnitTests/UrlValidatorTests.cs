using Encurtador.Application.Links;
using Xunit;

namespace Encurtador.UnitTests;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("http://exemplo.com")]
    [InlineData("https://exemplo.com/pagina?x=1")]
    public void Accepts_http_and_https_urls(string url)
    {
        Assert.Null(UrlValidator.Validate(url));
    }

    [Fact]
    public void Accepts_url_with_exactly_2048_characters()
    {
        var url = "https://exemplo.com/" + new string('a', 2048 - "https://exemplo.com/".Length);

        Assert.Equal(2048, url.Length);
        Assert.Null(UrlValidator.Validate(url));
    }

    [Fact]
    public void Rejects_url_with_2049_characters()
    {
        var url = "https://exemplo.com/" + new string('a', 2049 - "https://exemplo.com/".Length);

        var error = UrlValidator.Validate(url);

        Assert.NotNull(error);
        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Theory]
    [InlineData("ftp://exemplo.com/arquivo")]
    [InlineData("javascript:alert(1)")]
    [InlineData("exemplo.com/sem-esquema")]
    [InlineData("https://")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_unsupported_or_missing_urls(string? url)
    {
        var error = UrlValidator.Validate(url);

        Assert.NotNull(error);
        Assert.Equal(ErrorKind.Validation, error.Kind);
    }
}
