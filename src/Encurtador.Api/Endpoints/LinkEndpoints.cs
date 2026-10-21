using Encurtador.Api.Http;
using Encurtador.Api.Models;
using Encurtador.Application.Links;
using LinkOptions = Encurtador.Application.Links.LinkOptions;
using Microsoft.AspNetCore.Mvc;

namespace Encurtador.Api.Endpoints;

// Único arquivo de endpoints do recurso "link". Só lê a requisição, chama o LinkService e mapeia o resultado.
public static class LinkEndpoints
{
    private const string ManagementKeyHeader = "X-Management-Key";
    private const string UnknownOrigin = "desconhecido";

    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/links", CreateLinkAsync)
            .WithName("createLink")
            .WithSummary("Criar link curto")
            .WithDescription("Cria um link curto para a URL de destino. Pode receber um código personalizado e uma data de expiração. Devolve a chave de gerenciamento, que só aparece nesta resposta.")
            .Produces<CreatedLinkResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        app.MapGet("/api/links/{code}", GetLinkStatsAsync)
            .WithName("getLinkStats")
            .WithSummary("Consultar estatísticas do link")
            .WithDescription("Devolve os dados do link e o total de acessos. Exige o cabeçalho X-Management-Key com a chave recebida na criação.")
            .Produces<LinkStatsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/{code}", RedirectLinkAsync)
            .WithName("redirectLink")
            .WithSummary("Acessar link curto")
            .WithDescription("Redireciona para a URL de destino (302) e registra um acesso. Um código fora do formato também resulta em 404. Links expirados respondem 410.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        return app;
    }

    private static async Task<IResult> CreateLinkAsync(
        CreateLinkRequest request,
        HttpContext http,
        LinkService service,
        LinkOptions options,
        CancellationToken ct)
    {
        var origin = http.Connection.RemoteIpAddress?.ToString() ?? UnknownOrigin;
        var command = new CreateLinkCommand(origin, request.TargetUrl, request.CustomCode, request.ExpiresAt);

        var result = await service.CreateAsync(command, ct);
        if (!result.IsSuccess)
        {
            return ProblemMapper.ToResult(result.Error!);
        }

        var created = result.Value!;
        var shortUrl = $"{options.PublicBaseUrl.TrimEnd('/')}/{created.Code}";
        var body = new CreatedLinkResponse(
            created.Code,
            shortUrl,
            created.TargetUrl,
            created.CreatedAtUtc,
            created.ExpiresAtUtc,
            created.ManagementKey);

        return Results.Created(shortUrl, body);
    }

    private static async Task<IResult> GetLinkStatsAsync(
        string code,
        [FromHeader(Name = ManagementKeyHeader)] string? managementKey,
        LinkService service,
        CancellationToken ct)
    {
        var result = await service.GetStatsAsync(code, managementKey, ct);
        if (!result.IsSuccess)
        {
            return ProblemMapper.ToResult(result.Error!);
        }

        var stats = result.Value!;
        return Results.Ok(new LinkStatsResponse(
            stats.Code,
            stats.TargetUrl,
            stats.CreatedAtUtc,
            stats.ExpiresAtUtc,
            stats.ClickCount));
    }

    private static async Task<IResult> RedirectLinkAsync(
        string code,
        HttpContext http,
        LinkService service,
        CancellationToken ct)
    {
        var outcome = await service.RegisterAccessAsync(code, ct);

        switch (outcome)
        {
            case Redirected redirected:
                // Cada acesso precisa passar pelo sistema, então nada de cache (REQ-016).
                http.Response.Headers.CacheControl = "no-store";
                return Results.Redirect(redirected.TargetUrl);

            case Expired:
                return ProblemMapper.ToResult(new LinkError(
                    ErrorKind.Expired,
                    "Este link expirou e não está mais disponível."));

            default:
                return ProblemMapper.ToResult(new LinkError(
                    ErrorKind.NotFound,
                    "Nenhum link foi encontrado com este código."));
        }
    }
}
