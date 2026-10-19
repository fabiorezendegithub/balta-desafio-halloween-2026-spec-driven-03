using Encurtador.Application.Links;
using Encurtador.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Encurtador.Infrastructure;

public static class DependencyInjection
{
    // Único ponto da aplicação onde o provider de banco é escolhido (Princípio III).
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        var connectionString = configuration["Database:ConnectionString"];

        Action<DbContextOptionsBuilder> configureProvider = provider switch
        {
            _ when string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) =>
                options => options.UseSqlite(connectionString),
            _ => throw new InvalidOperationException(
                $"Provider de banco desconhecido: '{provider}'. Valor esperado: 'Sqlite'."),
        };

        services.AddDbContext<AppDbContext>(configureProvider);
        services.AddScoped<ILinkRepository, EfLinkRepository>();

        return services;
    }
}
