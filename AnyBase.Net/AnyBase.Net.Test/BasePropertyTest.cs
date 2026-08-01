using FsCheck;

namespace AnyBase.Net.Test;

public class BasePropertyTest
{
    private const string CandidateSymbols =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";

    [Test]
    public void Bytes_RoundTrip_WithGeneratedAlphabetAndSize()
    {
        var property = FsCheck.Fluent.Prop.ForAll<(
            byte[] Bytes,
            int AlphabetSeed,
            byte RadixSeed,
            byte PaddingSeed)>(input =>
        {
            var bytes = input.Bytes ?? Array.Empty<byte>();
            var radix = 2 + input.RadixSeed % 63;
            var alphabet = CreateAlphabet(input.AlphabetSeed, radix);
            var sut = new Base<char>(alphabet);
            sut.Size += input.PaddingSeed % 5;

            var encoded = sut.Encode(bytes);
            var decoded = sut.DecodeToBytes(encoded);

            return encoded.Length == bytes.Length * sut.Size && decoded.SequenceEqual(bytes);
        });

        Check.One(Config.QuickThrowOnFailure.WithMaxTest(300), property);
    }

    private static string CreateAlphabet(int seed, int length)
    {
        var symbols = CandidateSymbols.ToCharArray();
        var random = new Random(seed);
        for (var index = symbols.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (symbols[index], symbols[swapIndex]) = (symbols[swapIndex], symbols[index]);
        }

        return new string(symbols, 0, length);
    }
}
