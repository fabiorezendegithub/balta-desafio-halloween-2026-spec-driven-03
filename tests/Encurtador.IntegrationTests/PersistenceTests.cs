using System.Net;
using System.Net.Http.Json;
using Encurtador.Api.Models;
using Xunit;

namespace Encurtador.IntegrationTests;

// Reinício da aplicação sobre o mesmo arquivo SQLite: links e contadores precisam sobreviver (REQ-014, SC-003).
public class PersistenceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"encurtador-persist-{Guid.NewGuid():N}.db");

    private const string Target = "https://exemplo.com/persistente";

    [Fact]
    public async Task Links_and_counters_survive_an_application_restart()
    {
        CreatedLinkResponse created;

        await using (var first = new LinkApiFactory(_databasePath))
        {
            var client = first.NoRedirectClient();
            created = await TestHttp.CreateLinkAsync(client, new
            {
                targetUrl = Target,
                customCode = "persiste1",
            });

            for (var i = 0; i < 5; i++)
            {
                await client.GetAsync("/persiste1");
            }
        }

        await using var second = new LinkApiFactory(_databasePath);
        var restarted = second.NoRedirectClient();

        using var statsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/links/persiste1");
        statsRequest.Headers.Add(TestHttp.ManagementKeyHeader, created.ManagementKey);
        var statsResponse = await restarted.SendAsync(statsRequest);

        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        var stats = await statsResponse.Content.ReadFromJsonAsync<LinkStatsResponse>();
        Assert.Equal("persiste1", stats!.Code);
        Assert.Equal(Target, stats.TargetUrl);
        Assert.Equal(created.CreatedAt, stats.CreatedAt);
        Assert.Equal(5, stats.ClickCount);

        var redirect = await restarted.GetAsync("/persiste1");
        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(Target, redirect.Headers.Location?.ToString());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _databasePath, _databasePath + "-shm", _databasePath + "-wal" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
