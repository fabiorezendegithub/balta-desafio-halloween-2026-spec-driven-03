namespace Encurtador.Application.Links;

public abstract record AccessOutcome;

public sealed record Redirected(string TargetUrl) : AccessOutcome;

public sealed record Expired : AccessOutcome;

public sealed record NotFound : AccessOutcome;
