using Encurtador.Application.Links;
using Encurtador.UnitTests.Fakes;
using Xunit;

namespace Encurtador.UnitTests;

public class LinkServiceRedirectTests
{
    private const string TargetUrl = "https://exemplo.com/destino";

    private static async Task<(InMemoryLinkRepository Repository, LinkService Service, FixedTimeProvider Time)> Setup(
        DateTimeOffset? expiresAt)
    {
        var repository = new InMemoryLinkRepository();
        var time = new FixedTimeProvider(TestServices.Now);
        var service = TestServices.Create(repository, time: time);

        var created = await service.CreateAsync(
            new CreateLinkCommand("origem", TargetUrl, "ativo123", expiresAt),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        return (repository, service, time);
    }

    [Fact]
    public async Task Active_link_redirects_to_target()
    {
        var (_, service, _) = await Setup(expiresAt: null);

        var outcome = await service.RegisterAccessAsync("ativo123", CancellationToken.None);

        Assert.Equal(new Redirected(TargetUrl), outcome);
    }

    [Fact]
    public async Task Expired_link_does_not_redirect_and_does_not_count()
    {
        var expiresAt = TestServices.Now.AddHours(1);
        var (repository, service, time) = await Setup(expiresAt);
        time.Advance(TimeSpan.FromHours(2));

        var outcome = await service.RegisterAccessAsync("ativo123", CancellationToken.None);

        Assert.IsType<Expired>(outcome);
        Assert.Equal(0, repository.All.Single().ClickCount);
    }

    [Fact]
    public async Task Unknown_code_is_not_found_and_does_not_count()
    {
        var (repository, service, _) = await Setup(expiresAt: null);

        var outcome = await service.RegisterAccessAsync("naoexiste", CancellationToken.None);

        Assert.IsType<NotFound>(outcome);
        Assert.Equal(0, repository.All.Single().ClickCount);
    }

    [Fact]
    public async Task Malformed_code_is_not_found_without_touching_the_repository()
    {
        var (repository, service, _) = await Setup(expiresAt: null);

        var outcome = await service.RegisterAccessAsync("ab", CancellationToken.None);

        Assert.IsType<NotFound>(outcome);
        Assert.Equal(0, repository.All.Single().ClickCount);
    }

    [Fact]
    public async Task Link_is_expired_exactly_at_its_expiry_instant()
    {
        var expiresAt = TestServices.Now.AddMinutes(30);
        var (repository, service, time) = await Setup(expiresAt);
        time.Advance(TimeSpan.FromMinutes(30));

        var outcome = await service.RegisterAccessAsync("ativo123", CancellationToken.None);

        Assert.IsType<Expired>(outcome);
        Assert.Equal(0, repository.All.Single().ClickCount);
    }

    [Fact]
    public async Task One_access_to_an_active_link_increments_the_counter_by_exactly_one()
    {
        var (repository, service, _) = await Setup(expiresAt: null);

        await service.RegisterAccessAsync("ativo123", CancellationToken.None);

        Assert.Equal(1, repository.All.Single().ClickCount);
    }

    [Fact]
    public async Task Three_accesses_increment_the_counter_by_three()
    {
        var (repository, service, _) = await Setup(expiresAt: null);

        for (var i = 0; i < 3; i++)
        {
            await service.RegisterAccessAsync("ativo123", CancellationToken.None);
        }

        Assert.Equal(3, repository.All.Single().ClickCount);
    }
}
