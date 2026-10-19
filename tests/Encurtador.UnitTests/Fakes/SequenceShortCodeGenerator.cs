using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Fakes;

// Gera códigos de uma sequência fixa, para simular colisões.
public sealed class SequenceShortCodeGenerator(params string[] codes) : IShortCodeGenerator
{
    private readonly Queue<string> _codes = new(codes);

    public string Generate() => _codes.Dequeue();
}
