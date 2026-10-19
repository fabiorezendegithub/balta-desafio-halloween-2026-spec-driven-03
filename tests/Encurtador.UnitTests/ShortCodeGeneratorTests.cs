using Encurtador.Application.Links;
using Xunit;

namespace Encurtador.UnitTests;

public class ShortCodeGeneratorTests
{
    private const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";
    private const string Ambiguous = "01ilo";

    [Fact]
    public void Generates_codes_with_exactly_seven_characters()
    {
        var code = new ShortCodeGenerator().Generate();

        Assert.Equal(7, code.Length);
    }

    [Fact]
    public void Every_generated_character_belongs_to_the_alphabet()
    {
        var generator = new ShortCodeGenerator();

        for (var i = 0; i < 1_000; i++)
        {
            var code = generator.Generate();
            Assert.All(code, c => Assert.Contains(c, Alphabet));
        }
    }

    [Fact]
    public void Ten_thousand_codes_never_contain_ambiguous_characters()
    {
        var generator = new ShortCodeGenerator();

        for (var i = 0; i < 10_000; i++)
        {
            var code = generator.Generate();
            Assert.DoesNotContain(code, c => Ambiguous.Contains(c));
        }
    }
}
