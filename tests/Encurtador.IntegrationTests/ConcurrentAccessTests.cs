using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Encurtador.IntegrationTests;

public class ConcurrentAccessTests
{
    [Fact]
    public async Task One_hundred_simultaneous_accesses_all_redirect_and_the_counter_ends_at_100()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.NoRedirectClient();
        await TestHttp.CreateLinkAsync(client, new { targetUrl = "https://exemplo.com/destino", customCode = "conc1234" });

        var responses = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => client.GetAsync("/conc1234")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Found, r.StatusCode));

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<Encurtador.Application.Links.ILinkRepository>();
        var link = await repository.FindByCodeKeyAsync("conc1234", CancellationToken.None);
        Assert.Equal(100, link!.ClickCount);
    }
}
