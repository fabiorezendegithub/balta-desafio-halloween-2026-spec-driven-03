using Encurtador.Application.Links;
using Encurtador.UnitTests.Fakes;
using Xunit;

namespace Encurtador.UnitTests;

public class LinkServiceStatsTests
{
    private const string TargetUrl = "https://exemplo.com/destino";

    private static async Task<(InMemoryLinkRepository Repository, LinkService Service, CreatedLink Created, FixedTimeProvider Time)>
        CreateLink(DateTimeOffset? expiresAt = null)
    {
        var repository = new InMemoryLinkRepository();
        var time = new FixedTimeProvider(TestServices.Now);
        var service = TestServices.Create(repository, time: time);

        var created = await service.CreateAsync(
            new CreateLinkCommand("origem", TargetUrl, "stats123", expiresAt),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        return (repository, service, created.Value!, time);
    }

    [Fact]
    public async Task Correct_key_returns_code_target_dates_and_total()
    {
        var (_, service, created, _) = await CreateLink();

        var result = await service.GetStatsAsync("stats123", created.ManagementKey, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("stats123", result.Value!.Code);
        Assert.Equal(TargetUrl, result.Value.TargetUrl);
        Assert.Equal(TestServices.Now, result.Value.CreatedAtUtc);
        Assert.Null(result.Value.ExpiresAtUtc);
        Assert.Equal(0, result.Value.ClickCount);
    }

    [Fact]
    public async Task Wrong_key_of_valid_format_is_unauthorized()
    {
        var (_, service, _, _) = await CreateLink();
        var wrongKey = ManagementKey.Generate();

        var result = await service.GetStatsAsync("stats123", wrongKey, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Unauthorized, result.Error!.Kind);
    }

    [Fact]
    public async Task Unknown_code_is_not_found()
    {
        var (_, service, created, _) = await CreateLink();

        var result = await service.GetStatsAsync("naoexiste", created.ManagementKey, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Missing_key_is_a_validation_error()
    {
        var (_, service, _, _) = await CreateLink();

        var result = await service.GetStatsAsync("stats123", null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task Malformed_key_is_a_validation_error()
    {
        var (_, service, _, _) = await CreateLink();

        var result = await service.GetStatsAsync("stats123", "curta", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task Expired_link_stays_queryable_with_the_same_total()
    {
        var (_, service, created, time) = await CreateLink(expiresAt: TestServices.Now.AddHours(1));
        await service.RegisterAccessAsync("stats123", CancellationToken.None);
        time.Advance(TimeSpan.FromHours(2));

        var result = await service.GetStatsAsync("stats123", created.ManagementKey, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.ClickCount);
    }

    [Fact]
    public async Task Total_grows_by_exactly_the_number_of_new_accesses_and_never_decreases()
    {
        var (_, service, created, _) = await CreateLink();

        await service.RegisterAccessAsync("stats123", CancellationToken.None);
        await service.RegisterAccessAsync("stats123", CancellationToken.None);
        var before = (await service.GetStatsAsync("stats123", created.ManagementKey, CancellationToken.None)).Value!.ClickCount;

        for (var i = 0; i < 4; i++)
        {
            await service.RegisterAccessAsync("stats123", CancellationToken.None);
        }

        var after = (await service.GetStatsAsync("stats123", created.ManagementKey, CancellationToken.None)).Value!.ClickCount;

        Assert.Equal(2, before);
        Assert.Equal(before + 4, after);
    }
}
