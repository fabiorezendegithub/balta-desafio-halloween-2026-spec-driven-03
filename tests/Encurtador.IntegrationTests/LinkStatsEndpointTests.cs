using System.Net;
using System.Net.Http.Json;
using Encurtador.Application.Links;
using Xunit;

namespace Encurtador.IntegrationTests;

public class LinkStatsEndpointTests
{
    private const string Target = "https://exemplo.com/destino";

    [Fact]
    public async Task Correct_key_returns_200_with_link_data_and_total()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        var created = await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "stats123" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/links/stats123");
        request.Headers.Add(TestHttp.ManagementKeyHeader, created.ManagementKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Api.Models.LinkStatsResponse>();
        Assert.NotNull(body);
        Assert.Equal("stats123", body.Code);
        Assert.Equal(Target, body.TargetUrl);
        Assert.Equal(created.CreatedAt, body.CreatedAt);
        Assert.Null(body.ExpiresAt);
        Assert.Equal(0, body.ClickCount);
    }

    [Fact]
    public async Task Missing_key_header_returns_400_problem_details()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "semchave1" });

        var response = await client.GetAsync("/api/links/semchave1");

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Malformed_key_returns_400_problem_details()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "formato12" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/links/formato12");
        request.Headers.Add(TestHttp.ManagementKeyHeader, "curta");
        var response = await client.SendAsync(request);

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Well_formed_but_wrong_key_returns_401_problem_details()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "errada123" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/links/errada123");
        request.Headers.Add(TestHttp.ManagementKeyHeader, ManagementKey.Generate());
        var response = await client.SendAsync(request);

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_code_returns_404_problem_details()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/links/naoexiste");
        request.Headers.Add(TestHttp.ManagementKeyHeader, ManagementKey.Generate());
        var response = await client.SendAsync(request);

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Total_grows_by_exactly_the_number_of_new_accesses()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        var created = await TestHttp.CreateLinkAsync(client, new { targetUrl = Target, customCode = "cresce12" });

        await client.GetAsync("/cresce12");
        await client.GetAsync("/cresce12");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/links/cresce12");
        request.Headers.Add(TestHttp.ManagementKeyHeader, created.ManagementKey);
        var body = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<Api.Models.LinkStatsResponse>();

        Assert.Equal(2, body!.ClickCount);
    }
}
