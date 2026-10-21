using System.Text.Json.Nodes;
using Encurtador.Api.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Encurtador.Api.Documentation;

// Liga o documento OpenAPI e a interface de testes. Sem regra de negócio e sem acesso a dados.
public static class ApiDocumentationExtensions
{
    private const string OpenApiRoutePattern = "/api/openapi/{documentName}.json";

    // /api/docs é a URL canônica. O Scalar redireciona para /api/docs/ (302), comportamento aceito.
    private const string InterfaceRoute = "/api/docs";

    private const string ManagementKeyHeader = "X-Management-Key";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer(ApplyExamples);
            options.AddOperationTransformer(DescribeHeaders);
        });

        return services;
    }

    // Documentation:Enabled explícito vence o padrão. Sem a chave, liga só em Development.
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        var enabled = app.Configuration.GetValue<bool?>("Documentation:Enabled") ?? app.Environment.IsDevelopment();
        if (!enabled)
        {
            return app;
        }

        app.MapOpenApi(OpenApiRoutePattern);
        app.MapScalarApiReference(InterfaceRoute, options => options.OpenApiRoutePattern = OpenApiRoutePattern);

        return app;
    }

    // Exemplos fictícios em português. Nenhum valor é chave real (REQ-025, REQ-026).
    private static Task ApplyExamples(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (context.JsonTypeInfo.Type == typeof(CreateLinkRequest))
        {
            schema.Examples = [new JsonObject
            {
                ["targetUrl"] = "https://www.exemplo.com.br/artigo/leitura-de-exemplo",
                ["customCode"] = "exemplo",
                ["expiresAt"] = "2027-01-01T00:00:00Z",
            }];
        }
        else if (context.JsonTypeInfo.Type == typeof(CreatedLinkResponse))
        {
            schema.Examples = [new JsonObject
            {
                ["code"] = "exemplo",
                ["shortUrl"] = "https://encurtador.exemplo.com.br/exemplo",
                ["targetUrl"] = "https://www.exemplo.com.br/artigo/leitura-de-exemplo",
                ["createdAt"] = "2026-01-01T12:00:00Z",
                ["expiresAt"] = null,
                ["managementKey"] = "chave-de-exemplo-nao-usar",
            }];
        }
        else if (context.JsonTypeInfo.Type == typeof(LinkStatsResponse))
        {
            schema.Examples = [new JsonObject
            {
                ["code"] = "exemplo",
                ["targetUrl"] = "https://www.exemplo.com.br/artigo/leitura-de-exemplo",
                ["createdAt"] = "2026-01-01T12:00:00Z",
                ["expiresAt"] = null,
                ["clickCount"] = 42,
            }];
        }

        return Task.CompletedTask;
    }

    // O OpenAPI nativo não documenta cabeçalhos de resposta nem descrição de parâmetro de cabeçalho.
    private static Task DescribeHeaders(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        foreach (var statusCode in new[] { "201", "302" })
        {
            if (operation.Responses is not null
                && operation.Responses.TryGetValue(statusCode, out var response)
                && response is OpenApiResponse concrete)
            {
                concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concrete.Headers["Location"] = new OpenApiHeader
                {
                    Description = statusCode == "201"
                        ? "URL curta criada."
                        : "URL de destino do link. O navegador segue este endereço.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };
            }
        }

        var keyParameter = operation.Parameters?.FirstOrDefault(p => p.Name == ManagementKeyHeader);
        if (keyParameter is not null)
        {
            keyParameter.Description = "Chave de gerenciamento devolvida na criação do link.";
        }

        return Task.CompletedTask;
    }
}
