namespace Encurtador.Application.Links;

public static class ReservedCodes
{
    private static readonly HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "health",
        "healthz",
        "ready",
        "live",
        "swagger",
        "openapi",
        "admin",
        "links",
        "metrics",
    };

    public static bool Contains(string code) => Codes.Contains(code);
}
