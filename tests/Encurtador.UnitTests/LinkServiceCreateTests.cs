using Encurtador.Application.Links;
using Encurtador.UnitTests.Fakes;
using Xunit;

namespace Encurtador.UnitTests;

public class LinkServiceCreateTests
{
    private const string TargetUrl = "https://exemplo.com/pagina-longa";
    private static readonly DateTimeOffset Future = TestServices.Now.AddDays(7);

    private static CreateLinkCommand Command(
        string? customCode = null,
        DateTimeOffset? expiresAt = null,
        string? targetUrl = TargetUrl) =>
        new("203.0.113.7", targetUrl, customCode, expiresAt);

    [Theory]
    [InlineData("abcd")]
    [InlineData("MeuLink")]
    [InlineData("ABCDEFGHIJKLMNOPQRST")]
    public async Task Accepts_custom_codes_from_4_to_20_alphanumeric_characters(string code)
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(customCode: code), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(code, result.Value!.Code);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("abc-123")]
    public async Task Rejects_custom_codes_outside_the_format(string code)
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(customCode: code), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(repository.All);
    }

    [Theory]
    [InlineData("Health")]
    [InlineData("api")]
    [InlineData("SWAGGER")]
    public async Task Rejects_reserved_codes_regardless_of_case(string code)
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(customCode: code), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task Rejects_expiry_in_the_past()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(
            Command(expiresAt: TestServices.Now.AddSeconds(-1)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(repository.All);
    }

    [Fact]
    public async Task Accepts_future_expiry()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(expiresAt: Future), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Future, result.Value!.ExpiresAtUtc);
    }

    [Fact]
    public async Task Link_without_expiry_has_null_expiry()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ExpiresAtUtc);
        Assert.Null(repository.All.Single().ExpiresAtUtc);
    }

    [Fact]
    public async Task Custom_code_already_in_use_is_rejected_and_existing_link_is_untouched()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);
        var first = await service.CreateAsync(Command(customCode: "meulink"), CancellationToken.None);
        var existing = repository.All.Single();

        var second = await service.CreateAsync(
            Command(customCode: "outraurl1") with { TargetUrl = "https://outro.com" },
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var clash = await service.CreateAsync(
            Command(customCode: "meulink") with { TargetUrl = "https://outro.com" },
            CancellationToken.None);

        Assert.False(clash.IsSuccess);
        Assert.Equal(ErrorKind.Validation, clash.Error!.Kind);
        Assert.Equal(TargetUrl, existing.TargetUrl);
        Assert.Equal(2, repository.All.Count);
    }

    [Fact]
    public async Task Codes_differing_only_by_case_are_the_same_key()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var first = await service.CreateAsync(Command(customCode: "MeuLink"), CancellationToken.None);
        var second = await service.CreateAsync(Command(customCode: "meulink"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorKind.Validation, second.Error!.Kind);
    }

    [Fact]
    public async Task Generated_code_collision_is_retried_until_a_free_code_is_found()
    {
        var repository = new InMemoryLinkRepository();
        var taken = await TestServices.Create(repository).CreateAsync(
            Command(customCode: "colisao1"),
            CancellationToken.None);
        Assert.True(taken.IsSuccess);

        var generator = new SequenceShortCodeGenerator("colisao1", "colisao1", "livre123");
        var service = TestServices.Create(repository, generator: generator);

        var result = await service.CreateAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("livre123", result.Value!.Code);
        Assert.Equal(2, repository.All.Count);
    }

    [Fact]
    public async Task Generated_code_collision_fails_after_five_attempts()
    {
        var repository = new InMemoryLinkRepository();
        await TestServices.Create(repository).CreateAsync(Command(customCode: "colisao1"), CancellationToken.None);

        var generator = new SequenceShortCodeGenerator(
            "colisao1", "colisao1", "colisao1", "colisao1", "colisao1");
        var service = TestServices.Create(repository, generator: generator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Rate_limited_origin_gets_rate_limited_error_and_nothing_is_inserted()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository, limiter: new FakeCreationLimiter(allow: false));

        var result = await service.CreateAsync(Command(customCode: "naocria1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.RateLimited, result.Error!.Kind);
        Assert.Equal(0, repository.InsertAttempts);
        Assert.Empty(repository.All);
    }

    [Fact]
    public async Task Allowed_origin_proceeds_with_creation()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository, limiter: new FakeCreationLimiter(allow: true));

        var result = await service.CreateAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.InsertAttempts);
    }

    [Theory]
    [InlineData("ftp://exemplo.com")]
    [InlineData("javascript:alert(1)")]
    public async Task Invalid_target_url_is_rejected_before_insert(string targetUrl)
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(targetUrl: targetUrl), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, repository.InsertAttempts);
    }

    [Fact]
    public async Task Created_link_returns_plain_management_key_that_matches_the_stored_hash()
    {
        var repository = new InMemoryLinkRepository();
        var service = TestServices.Create(repository);

        var result = await service.CreateAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(43, result.Value!.ManagementKey.Length);
        Assert.True(ManagementKey.Verify(result.Value.ManagementKey, repository.All.Single().ManagementKeyHash));
        Assert.NotEqual(result.Value.ManagementKey, repository.All.Single().ManagementKeyHash);
    }
}
