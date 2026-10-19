namespace Encurtador.Application.Links;

public sealed class Link
{
    // Construtor usado pelo EF Core ao materializar a entidade.
    private Link()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        TargetUrl = string.Empty;
        ManagementKeyHash = string.Empty;
    }

    public Link(
        string code,
        string targetUrl,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc,
        string managementKeyHash)
    {
        Code = code;
        CodeKey = code.ToLowerInvariant();
        TargetUrl = targetUrl;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
        ManagementKeyHash = managementKeyHash;
    }

    public long Id { get; private set; }

    public string Code { get; private set; }

    public string CodeKey { get; private set; }

    public string TargetUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public long ClickCount { get; private set; }

    public string ManagementKeyHash { get; private set; }

    // No instante exato de ExpiresAtUtc, o link já é considerado expirado.
    public bool IsExpiredAt(DateTimeOffset nowUtc) =>
        ExpiresAtUtc is { } expiresAt && expiresAt <= nowUtc;

    // Regra de um acesso válido: só conta se o link estiver ativo. A persistência aplica a mesma
    // regra em UPDATE atômico (D2); este método é o equivalente de domínio, usado pela fake em memória.
    public bool TryRegisterClickAt(DateTimeOffset nowUtc)
    {
        if (IsExpiredAt(nowUtc))
        {
            return false;
        }

        ClickCount++;
        return true;
    }
}
