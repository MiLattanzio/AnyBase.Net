using System.Text;

namespace AnyBase.Net.Test;

public class BaseTest
{
    private static readonly Base<char> Binary = new("01");
    private static readonly Base<char> Octal = new("01234567");
    private static readonly Base<char> Decimal = new("0123456789");
    private static readonly Base<char> Hex = new("0123456789ABCDEF");

    [TestCase("01", "01000001")]
    [TestCase("01234567", "101")]
    [TestCase("0123456789", "065")]
    [TestCase("0123456789ABCDEF", "41")]
    public void EncodeToString_KnownByte_UsesFixedWidth(string alphabet, string expected)
    {
        var sut = new Base<char>(alphabet);

        var encoded = sut.EncodeToString(new byte[] { 65 });

        Assert.That(encoded, Is.EqualTo(expected));
    }

    [TestCase("01")]
    [TestCase("01234567")]
    [TestCase("0123456789")]
    [TestCase("0123456789ABCDEF")]
    [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/")]
    public void Text_RoundTripsUtf8_InEveryAlphabet(string alphabet)
    {
        var sut = new Base<char>(alphabet);
        const string value = "Plant trees 🌳 — piantiamo alberi";

        var encoded = sut.EncodeToString(value);
        var decoded = sut.DecodeToString(encoded);

        Assert.That(decoded, Is.EqualTo(value));
    }

    [TestCase("01")]
    [TestCase("01234567")]
    [TestCase("0123456789")]
    [TestCase("0123456789ABCDEF")]
    public void Bytes_RoundTripEveryPossibleValue(string alphabet)
    {
        var sut = new Base<char>(alphabet);
        var bytes = Enumerable.Range(byte.MinValue, byte.MaxValue + 1).Select(value => (byte)value).ToArray();

        var decoded = sut.DecodeToBytes(sut.Encode(bytes));

        Assert.That(decoded, Is.EqualTo(bytes));
    }

    [Test]
    public void StringAndSymbolArrayDecoding_AreEquivalent()
    {
        const string value = "AnyBase.Net";
        var symbols = Hex.Encode(value);

        Assert.Multiple(() =>
        {
            Assert.That(Hex.DecodeToString(symbols), Is.EqualTo(value));
            Assert.That(Hex.DecodeToString(string.Concat(symbols)), Is.EqualTo(value));
        });
    }

    [Test]
    public void EmptyInputs_HaveDefinedRoundTrips()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Binary.Encode(Array.Empty<byte>()), Is.Empty);
            Assert.That(Binary.Encode(string.Empty), Is.Empty);
            Assert.That(Binary.EncodeToString(string.Empty), Is.Empty);
            Assert.That(Binary.DecodeToBytes(Array.Empty<char>()), Is.Empty);
            Assert.That(Binary.DecodeToString(Array.Empty<char>()), Is.Empty);
            Assert.That(Binary.DecodeToString(string.Empty), Is.Empty);
        });
    }

    [Test]
    public void Constructor_PreservesExplicitAlphabetOrder()
    {
        var sut = new Base<char>(new[] { 'Z', 'A' });

        Assert.Multiple(() =>
        {
            Assert.That(sut.Identity, Is.EqualTo(new[] { 'Z', 'A' }));
            Assert.That(sut.EncodeToString(new byte[] { 0, 1 }), Is.EqualTo("ZZZZZZZZZZZZZZZA"));
        });
    }

    [Test]
    public void Constructor_RejectsInvalidAlphabets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => new Base<char>((IEnumerable<char>)null!),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new Base<char>(Array.Empty<char>()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new Base<char>(new[] { '0' }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new Base<char>(new[] { '0', '1', '0' }),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void Operations_RejectNullInputs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => Hex.Encode((byte[])null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Hex.Encode((string)null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Hex.EncodeToString((byte[])null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Hex.EncodeToString((string)null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Hex.DecodeToBytes((char[])null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Hex.DecodeToString((char[])null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Hex.DecodeToString((string)null!), Throws.TypeOf<ArgumentNullException>());
        });
    }

    [Test]
    public void Decode_RejectsMalformedInput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => Hex.DecodeToBytes("4".ToCharArray()), Throws.TypeOf<FormatException>());
            Assert.That(() => Hex.DecodeToBytes("GG".ToCharArray()), Throws.TypeOf<FormatException>());
            Assert.That(() => Decimal.DecodeToBytes("999".ToCharArray()), Throws.TypeOf<FormatException>());
            Assert.That(() => Hex.DecodeToString("4Z"), Throws.TypeOf<FormatException>());
        });
    }

    [Test]
    public void DecodeErrors_ReportGroupSymbolAndTextPositions()
    {
        var incomplete = Assert.Throws<FormatException>(() => Hex.DecodeToBytes("4".ToCharArray()));
        var unknownSymbol = Assert.Throws<FormatException>(() => Hex.DecodeToBytes("4G".ToCharArray()));
        var overflow = Assert.Throws<FormatException>(() => Decimal.DecodeToBytes("999".ToCharArray()));
        var unknownText = Assert.Throws<FormatException>(() => Hex.DecodeToString("4Z"));

        Assert.Multiple(() =>
        {
            Assert.That(incomplete!.Message, Does.Contain("symbol count 1"));
            Assert.That(incomplete.Message, Does.Contain("byte group 0"));
            Assert.That(incomplete.Message, Does.Contain("symbol index 0"));
            Assert.That(unknownSymbol!.Message, Does.Contain("'G'"));
            Assert.That(unknownSymbol.Message, Does.Contain("symbol index 1"));
            Assert.That(unknownSymbol.Message, Does.Contain("byte group 0, offset 1"));
            Assert.That(overflow!.Message, Does.Contain("byte group 0 exceeds 255"));
            Assert.That(overflow.Message, Does.Contain("symbol index 2"));
            Assert.That(unknownText!.Message, Does.Contain("text position 1"));
            Assert.That(unknownText.Message, Does.Contain("'Z'"));
        });
    }

    [Test]
    public void DecodeToString_RejectsInvalidUtf8()
    {
        var encoded = Binary.Encode(new byte[] { 0xFF });
        var exception = Assert.Throws<DecoderFallbackException>(() => Binary.DecodeToString(encoded));

        Assert.That(exception!.Message, Does.Contain("byte index 0"));
    }

    [Test]
    public void Size_CanAddPaddingButCannotLoseBytePrecision()
    {
        var sut = new Base<char>("0123456789ABCDEF") { Size = 4 };

        var encoded = sut.EncodeToString(new byte[] { 65 });

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Is.EqualTo("0041"));
            Assert.That(sut.DecodeToBytes(encoded.ToCharArray()), Is.EqualTo(new byte[] { 65 }));
            Assert.That(() => sut.Size = 1, Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void MultiCharacterSymbols_CanBeDecodedFromText()
    {
        var sut = new Base<string>(new[] { "zero", "one" });
        const string value = "A";

        var encoded = sut.EncodeToString(value);

        Assert.That(sut.DecodeToString(encoded), Is.EqualTo(value));
    }

    [Test]
    public void PrefixAmbiguousTextAlphabet_IsSupportedOnlyBySymbolArrayOperations()
    {
        var sut = new Base<string>(new[] { "a", "ab" });
        var bytes = Encoding.UTF8.GetBytes("A");

        var encodedSymbols = sut.Encode(bytes);
        var decodedBytes = sut.DecodeToBytes(encodedSymbols);
        var decodedText = sut.DecodeToString(encodedSymbols);
        var encodeBytesError = Assert.Throws<InvalidOperationException>(() => sut.EncodeToString(bytes));
        var encodeTextError = Assert.Throws<InvalidOperationException>(() => sut.EncodeToString("A"));
        var decodeTextError = Assert.Throws<InvalidOperationException>(() => sut.DecodeToString(string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(decodedBytes, Is.EqualTo(bytes));
            Assert.That(decodedText, Is.EqualTo("A"));
            Assert.That(encodeBytesError!.Message, Does.Contain("'a' at index 0 is a prefix of 'ab' at index 1"));
            Assert.That(encodeTextError!.Message, Does.Contain("prefix-free textual alphabet"));
            Assert.That(decodeTextError!.Message, Does.Contain("symbol-array APIs"));
        });
    }

    [Test]
    public void EmptyTextSymbol_IsSupportedOnlyBySymbolArrayOperations()
    {
        var sut = new Base<string>(new[] { string.Empty, "one" });
        var bytes = new byte[] { 0, 1, 255 };

        var encodedSymbols = sut.Encode(bytes);

        Assert.Multiple(() =>
        {
            Assert.That(sut.DecodeToBytes(encodedSymbols), Is.EqualTo(bytes));
            Assert.That(
                () => sut.EncodeToString(bytes),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("empty text representation"));
            Assert.That(
                () => sut.DecodeToString(string.Empty),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("symbol-array APIs"));
        });
    }
}
