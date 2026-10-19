using System.Net;
using Encurtador.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Encurtador.IntegrationTests;

// Valida a fiação entre endpoint, serviço e adaptador de limite. A regra em si está coberta pelos testes unitários.
public class RateLimitTests
{
    [Fact]
    public async Task Fourth_creation_from_the_same_origin_returns_429_and_creates_no_link()
    {
        await using var factory = new LinkApiFactory(creationLimitPerHour: 3);
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            await TestHttp.CreateLinkAsync(client, new { targetUrl = $"https://exemplo.com/{i}" });
        }

        var response = await client.PostAsync(
            "/api/links",
            TestHttp.Json(new { targetUrl = "https://exemplo.com/quarta" }));

        var problem = await TestHttp.AssertProblemAsync(response, HttpStatusCode.TooManyRequests);
        Assert.Contains("Limite de criações", problem.GetProperty("title").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.Links.CountAsync());
    }
}
