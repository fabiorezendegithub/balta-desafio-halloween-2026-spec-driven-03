namespace Encurtador.Application.Links;

public static class UrlValidator
{
    private const int MaxLength = 2048;

    public static LinkError? Validate(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return new LinkError(ErrorKind.Validation, "A URL de destino é obrigatória.");
        }

        if (targetUrl.Length > MaxLength)
        {
            return new LinkError(ErrorKind.Validation, "A URL de destino deve ter no máximo 2048 caracteres.");
        }

        var isAbsolute = Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri);
        var isHttp = isAbsolute
            && uri is not null
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);

        if (!isHttp)
        {
            return new LinkError(ErrorKind.Validation, "A URL de destino deve começar com http:// ou https://.");
        }

        return null;
    }
}
