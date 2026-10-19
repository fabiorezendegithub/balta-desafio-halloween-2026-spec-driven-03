using System.Security.Cryptography;

namespace Encurtador.Application.Links;

public sealed class ShortCodeGenerator : IShortCodeGenerator
{
    // Sem 0, 1, i, l e o, para evitar confusão visual.
    private const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";
    private const int CodeLength = 7;

    public string Generate()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
