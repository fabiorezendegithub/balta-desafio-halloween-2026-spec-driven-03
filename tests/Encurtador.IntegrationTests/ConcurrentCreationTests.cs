using System.Net;
using Xunit;

namespace Encurtador.IntegrationTests;

public class ConcurrentCreationTests
{
    [Fact]
    public async Task Twenty_simultaneous_creations_with_the_same_custom_code_yield_exactly_one_201()
    {
        await using var factory = new LinkApiFactory();
        var client = factory.CreateClient();

        var tasks = Enumerable.Range(0, 20).Select(i =>
            client.PostAsync("/api/links", TestHttp.Json(new
            {
                targetUrl = $"https://exemplo.com/{i}",
                customCode = "corrida01",
            })));

        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
    }
}
