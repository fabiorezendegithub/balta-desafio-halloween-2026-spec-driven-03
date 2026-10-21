using Encurtador.Api.Documentation;
using Encurtador.Api.Endpoints;
using Encurtador.Api.RateLimiting;
using Encurtador.Application.Links;
using LinkOptions = Encurtador.Application.Links.LinkOptions;
using Encurtador.Infrastructure;
using Encurtador.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddApiDocumentation(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var linkOptions = new LinkOptions(
    builder.Configuration["Links:PublicBaseUrl"]
        ?? throw new InvalidOperationException("Configuração 'Links:PublicBaseUrl' ausente."),
    builder.Configuration.GetValue<int>("Links:CreationLimitPerHour"));

builder.Services.AddSingleton(linkOptions);
builder.Services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddSingleton<ICreationLimiter, FixedWindowCreationLimiter>();
builder.Services.AddScoped<LinkService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapApiDocumentation();
app.MapLinkEndpoints();

app.Run();

// Torna Program acessível aos testes de integração (WebApplicationFactory<Program>).
public partial class Program { }
