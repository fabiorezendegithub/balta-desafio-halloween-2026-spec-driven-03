using Encurtador.Application.Links;
using Encurtador.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Encurtador.IntegrationTests;

// Prova as defesas do banco, sem passar pelo serviço.
public class DatabaseConstraintsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Inserting_two_links_with_the_same_code_key_fails_on_the_unique_index()
    {
        await using var factory = new LinkApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Links.Add(new Link("Teste123", "https://exemplo.com/a", Now, null, "hash-a"));
        db.Links.Add(new Link("teste123", "https://exemplo.com/b", Now, null, "hash-b"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Negative_click_count_fails_on_the_check_constraint()
    {
        await using var factory = new LinkApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Links.Add(new Link("negativo1", "https://exemplo.com", Now, null, "hash"));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<SqliteException>(() =>
            db.Links.Where(l => l.CodeKey == "negativo1")
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ClickCount, -1)));
    }
}
