using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace Encurtador.IntegrationTests;

public class LinkCreationEndpointTests
{
    [Fact]
    public async Task Create_without_custom_code_returns_201_with_generated_code_and_management_key()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/links", TestHttp.Json(new { targetUrl = "https://exemplo.com/longa" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Api.Models.CreatedLinkResponse>();
        Assert.NotNull(body);
        Assert.Equal(7, body.Code.Length);
        Assert.Equal($"http://localhost:5000/{body.Code}", body.ShortUrl);
        Assert.Equal(body.ShortUrl, response.Headers.Location?.ToString());
        Assert.Equal("https://exemplo.com/longa", body.TargetUrl);
        Assert.Equal(43, body.ManagementKey.Length);
        Assert.Null(body.ExpiresAt);
    }

    [Fact]
    public async Task Create_with_custom_code_returns_201_with_that_code()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.CreateClient();

        var body = await TestHttp.CreateLinkAsync(client, new
        {
            targetUrl = "https://exemplo.com/longa",
            customCode = "MeuLink",
        });

        Assert.Equal("MeuLink", body.Code);
        Assert.Equal("http://localhost:5000/MeuLink", body.ShortUrl);
    }

    [Theory]
    [InlineData("{\"targetUrl\":\"ftp://exemplo.com/arquivo\"}")]
    [InlineData("{\"targetUrl\":\"javascript:alert(1)\"}")]
    [InlineData("{\"customCode\":\"abc\",\"targetUrl\":\"https://exemplo.com\"}")]
    [InlineData("{\"customCode\":\"abc-123\",\"targetUrl\":\"https://exemplo.com\"}")]
    [InlineData("{\"customCode\":\"Health\",\"targetUrl\":\"https://exemplo.com\"}")]
    [InlineData("{\"expiresAt\":\"2020-01-01T00:00:00Z\",\"targetUrl\":\"https://exemplo.com\"}")]
    [InlineData("{\"targetUrl\":")]
    public async Task Invalid_input_returns_400_problem_details_and_creates_nothing(string json)
    {
        await using var factory = new LinkApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/links", new StringContent(json, Encoding.UTF8, "application/json"));

        await TestHttp.AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Custom_code_already_in_use_returns_400_problem_details()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.CreateClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = "https://exemplo.com/a", customCode = "emuso123" });

        var response = await client.PostAsync(
            "/api/links",
            TestHttp.Json(new { targetUrl = "https://exemplo.com/b", customCode = "EMUSO123" }));

        var problem = await TestHttp.AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains("já está em uso", problem.GetProperty("detail").GetString());
    }
}
