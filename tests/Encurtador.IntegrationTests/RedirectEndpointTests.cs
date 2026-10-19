using System.Net;
using Encurtador.Application.Links;
using Encurtador.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Encurtador.IntegrationTests;

public class RedirectEndpointTests
{
    private const string Target = "https://exemplo.com/destino";

    [Fact]
    public async Task Active_link_redirects_with_302_no_store_and_counts_the_access()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "redir123" });

        var response = await client.GetAsync("/redir123");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(Target, response.Headers.Location?.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(1, await ClickCountAsync(factory, "redir123"));
    }

    [Fact]
    public async Task Three_accesses_add_exactly_three_to_the_counter()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "tres1234" });

        for (var i = 0; i < 3; i++)
        {
            await client.GetAsync("/tres1234");
        }

        Assert.Equal(3, await ClickCountAsync(factory, "tres1234"));
    }

    [Fact]
    public async Task Uppercase_code_redirects_to_the_same_link()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "minusc12" });

        var response = await client.GetAsync("/MINUSC12");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(Target, response.Headers.Location?.ToString());
        Assert.Equal(1, await ClickCountAsync(factory, "minusc12"));
    }

    [Fact]
    public async Task Expired_link_returns_410_problem_details_and_does_not_count()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Encurtador.Infrastructure.Persistence.AppDbContext>();
            db.Links.Add(new Link(
                "expirou1",
                Target,
                DateTimeOffset.UtcNow.AddDays(-2),
                DateTimeOffset.UtcNow.AddDays(-1),
                ManagementKey.Hash(ManagementKey.Generate())));
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/expirou1");

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.Gone);
        Assert.Equal(0, await ClickCountAsync(factory, "expirou1"));
    }

    [Fact]
    public async Task Unknown_code_returns_404_problem_details()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();

        var response = await client.GetAsync("/naoexiste");

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Malformed_code_returns_404_without_validation_error()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();

        var response = await client.GetAsync("/ab");

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(3, "offsetp03")]
    [InlineData(-3, "offsetm03")]
    public async Task Expiry_with_non_utc_offset_is_stored_as_the_same_instant(int offsetHours, string code)
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(2).ToOffset(TimeSpan.FromHours(offsetHours));

        var created = await TestHttp.CreateLinkAsync(client, new
        {
            targetUrl = Target,
            customCode = code,
            expiresAt = expiresAt.ToString("o"),
        });

        Assert.NotNull(created.ExpiresAt);
        Assert.Equal(expiresAt.ToUniversalTime().ToUnixTimeSeconds(), created.ExpiresAt.Value.ToUnixTimeSeconds());

        var response = await client.GetAsync($"/{code}");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<long> ClickCountAsync(LinkApiFactory factory, string code)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILinkRepository>();
        var link = await repository.FindByCodeKeyAsync(code.ToLowerInvariant(), CancellationToken.None);
        return link!.ClickCount;
    }
}
