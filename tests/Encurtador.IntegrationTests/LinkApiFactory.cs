using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Encurtador.IntegrationTests;

// Sobe a aplicação real com SQLite em arquivo temporário. Sem arquivo informado, o arquivo é apagado no Dispose.
// environmentName e documentationEnabled são opcionais: sem eles, vale o padrão de testes (Development) e a configuração do projeto.
public sealed class LinkApiFactory(
    string? databasePath = null,
    int creationLimitPerHour = 1000,
    string? environmentName = null,
    bool? documentationEnabled = null) : WebApplicationFactory<Program>
{
    private readonly bool _ownsDatabase = databasePath is null;

    public string DatabasePath { get; } =
        databasePath ?? Path.Combine(Path.GetTempPath(), $"encurtador-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, e não ConfigureAppConfiguration: o WebApplicationBuilder (minimal hosting)
        // já montou a configuração antes do callback, então só as configurações de host chegam ao Program.
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:ConnectionString", $"Data Source={DatabasePath}");
        builder.UseSetting("Links:CreationLimitPerHour", creationLimitPerHour.ToString(CultureInfo.InvariantCulture));

        if (environmentName is not null)
        {
            builder.UseEnvironment(environmentName);
        }

        if (documentationEnabled is { } enabled)
        {
            builder.UseSetting("Documentation:Enabled", enabled ? "true" : "false");
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || !_ownsDatabase)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { DatabasePath, DatabasePath + "-shm", DatabasePath + "-wal" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
