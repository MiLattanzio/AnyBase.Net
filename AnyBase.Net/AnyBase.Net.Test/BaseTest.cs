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
            Assert.That(() => Hex.DecodeToBytes(null!), Throws.TypeOf<ArgumentNullException>());
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
    public void DecodeToString_RejectsInvalidUtf8()
    {
        var encoded = Binary.Encode(new byte[] { 0xFF });

        Assert.That(() => Binary.DecodeToString(encoded), Throws.TypeOf<DecoderFallbackException>());
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
}
