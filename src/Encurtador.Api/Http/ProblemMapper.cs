using Encurtador.Application.Links;
using Microsoft.AspNetCore.Http;

namespace Encurtador.Api.Http;

// Converte o erro tipado da aplicação em ProblemDetails (application/problem+json).
public static class ProblemMapper
{
    public static IResult ToResult(LinkError error)
    {
        var (status, title) = error.Kind switch
        {
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Dados inválidos"),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Link não encontrado"),
            ErrorKind.Expired => (StatusCodes.Status410Gone, "Link expirado"),
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Não autorizado"),
            ErrorKind.RateLimited => (StatusCodes.Status429TooManyRequests, "Limite de criações excedido"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Tipo de erro desconhecido."),
        };

        return Results.Problem(detail: error.Message, statusCode: status, title: title);
    }
}
