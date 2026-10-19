using Encurtador.Application.Links;
using Xunit;

namespace Encurtador.UnitTests;

public class ManagementKeyTests
{
    [Fact]
    public void Generated_key_has_43_base64url_characters()
    {
        var key = ManagementKey.Generate();

        Assert.Equal(43, key.Length);
        Assert.True(ManagementKey.IsWellFormed(key));
        Assert.DoesNotContain('=', key);
    }

    [Fact]
    public void Hash_is_64_hexadecimal_characters()
    {
        var hash = ManagementKey.Hash(ManagementKey.Generate());

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Verify_accepts_the_correct_key()
    {
        var key = ManagementKey.Generate();
        var hash = ManagementKey.Hash(key);

        Assert.True(ManagementKey.Verify(key, hash));
    }

    [Fact]
    public void Verify_rejects_another_key_of_the_same_length()
    {
        var key = ManagementKey.Generate();
        var other = ManagementKey.Generate();
        var hash = ManagementKey.Hash(key);

        Assert.Equal(key.Length, other.Length);
        Assert.False(ManagementKey.Verify(other, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    [InlineData("abc*def")]
    public void IsWellFormed_rejects_wrong_length_or_alphabet(string key)
    {
        Assert.False(ManagementKey.IsWellFormed(key));
    }
}
