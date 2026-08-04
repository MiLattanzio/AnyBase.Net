using System.Text;

namespace AnyBase.Net.Test;

public class PackedEncodingTest
{
    [TestCase("", "", "", "")]
    [TestCase("f", "66", "MY======", "Zg==")]
    [TestCase("fo", "666F", "MZXQ====", "Zm8=")]
    [TestCase("foo", "666F6F", "MZXW6===", "Zm9v")]
    [TestCase("foob", "666F6F62", "MZXW6YQ=", "Zm9vYg==")]
    [TestCase("fooba", "666F6F6261", "MZXW6YTB", "Zm9vYmE=")]
    [TestCase("foobar", "666F6F626172", "MZXW6YTBOI======", "Zm9vYmFy")]
    public void Rfc4648OfficialVectors_RoundTrip(
        string value,
        string expectedBase16,
        string expectedBase32,
        string expectedBase64)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var base16 = AnyBase.CreateRfc4648Base16();
        var base32 = AnyBase.CreateRfc4648Base32();
        var base64 = AnyBase.CreateRfc4648Base64();

        Assert.Multiple(() =>
        {
            Assert.That(base16.EncodeToString(bytes), Is.EqualTo(expectedBase16));
            Assert.That(base32.EncodeToString(bytes), Is.EqualTo(expectedBase32));
            Assert.That(base64.EncodeToString(bytes), Is.EqualTo(expectedBase64));
            Assert.That(base16.DecodeToBytes(expectedBase16), Is.EqualTo(bytes));
            Assert.That(base32.DecodeToBytes(expectedBase32), Is.EqualTo(bytes));
            Assert.That(base64.DecodeToBytes(expectedBase64), Is.EqualTo(bytes));
        });
    }

    [Test]
    public void Base64Packed_MatchesDotNetForDeterministicBinaryInputs()
    {
        var base16 = AnyBase.CreateRfc4648Base16();
        var sut = AnyBase.CreateRfc4648Base64();
        var urlSafe = AnyBase.CreateRfc4648Base64Url();
        var random = new Random(42);

        for (var length = 0; length <= 128; length++)
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            var expected = Convert.ToBase64String(bytes);
            var expectedUrlSafe = expected.Replace('+', '-').Replace('/', '_');

            Assert.That(base16.EncodeToString(bytes), Is.EqualTo(Convert.ToHexString(bytes)), $"Base16 length {length}");
            Assert.That(sut.EncodeToString(bytes), Is.EqualTo(expected), $"length {length}");
            Assert.That(sut.DecodeToBytes(expected), Is.EqualTo(bytes), $"length {length}");
            Assert.That(urlSafe.EncodeToString(bytes), Is.EqualTo(expectedUrlSafe), $"Base64 URL length {length}");
            Assert.That(urlSafe.DecodeToBytes(expectedUrlSafe), Is.EqualTo(bytes), $"Base64 URL length {length}");
        }
    }

    [Test]
    public void RfcFactories_AreDistinctFromHistoricalFixedWidthFactories()
    {
        var fixedWidth = AnyBase.CreateBase64();
        var packed = AnyBase.CreateRfc4648Base64();

        Assert.Multiple(() =>
        {
            Assert.That(fixedWidth.Mode, Is.EqualTo(EncodingMode.FixedWidthByte));
            Assert.That(packed.Mode, Is.EqualTo(EncodingMode.Packed));
            Assert.That(fixedWidth.EncodeToString("f"), Is.EqualTo("Bm"));
            Assert.That(packed.EncodeToString("f"), Is.EqualTo("Zg=="));
            Assert.That(packed.SupportsPadding, Is.True);
            Assert.That(packed.PaddingSymbol, Is.EqualTo('='));
            Assert.That(packed.UsePadding, Is.True);
        });
    }

    [Test]
    public void FixedWidthByte_RemainsTheDefaultAndMatchesExplicitMode()
    {
        var source = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var historical = AnyBase.CreateBase64();
        var explicitFixedWidth = AnyBase.Create(AnyBaseAlphabets.Base64, EncodingMode.FixedWidthByte);

        Assert.Multiple(() =>
        {
            Assert.That(historical.Mode, Is.EqualTo(EncodingMode.FixedWidthByte));
            Assert.That(explicitFixedWidth.Encode(source), Is.EqualTo(historical.Encode(source)));
            Assert.That(explicitFixedWidth.DecodeToBytes(historical.Encode(source)), Is.EqualTo(source));
        });
    }

    [Test]
    public void Padding_CanBeOmittedAndIsValidatedStrictly()
    {
        var padded = AnyBase.CreateRfc4648Base64();
        var unpadded = AnyBase.CreateRfc4648Base64(usePadding: false);

        Assert.Multiple(() =>
        {
            Assert.That(padded.EncodeToString("f"), Is.EqualTo("Zg=="));
            Assert.That(unpadded.EncodeToString("f"), Is.EqualTo("Zg"));
            Assert.That(unpadded.DecodeToString("Zg"), Is.EqualTo("f"));
            Assert.That(() => padded.DecodeToBytes("Zg"), Throws.TypeOf<FormatException>());
            Assert.That(() => unpadded.DecodeToBytes("Zg=="), Throws.TypeOf<FormatException>());
            Assert.That(() => padded.DecodeToBytes("Zg==="), Throws.TypeOf<FormatException>());
            Assert.That(() => padded.DecodeToBytes("Z=g="), Throws.TypeOf<FormatException>());
            Assert.That(() => padded.DecodeToBytes("Zh=="), Throws.TypeOf<FormatException>());
        });
    }

    [Test]
    public void PackedMode_PreservesLeadingZeroBytes()
    {
        var bytes = new byte[] { 0, 0, 1 };
        var base16 = AnyBase.CreateRfc4648Base16();
        var base32 = AnyBase.CreateRfc4648Base32();
        var base64 = AnyBase.CreateRfc4648Base64();

        Assert.Multiple(() =>
        {
            Assert.That(base16.EncodeToString(bytes), Is.EqualTo("000001"));
            Assert.That(base32.EncodeToString(bytes), Is.EqualTo("AAAAC==="));
            Assert.That(base64.EncodeToString(bytes), Is.EqualTo("AAAB"));
            Assert.That(base16.DecodeToBytes("000001"), Is.EqualTo(bytes));
            Assert.That(base32.DecodeToBytes("AAAAC==="), Is.EqualTo(bytes));
            Assert.That(base64.DecodeToBytes("AAAB"), Is.EqualTo(bytes));
        });
    }

    [Test]
    public void Base64Url_UsesRfcUrlSafeSymbols()
    {
        var bytes = new byte[] { 0xFB, 0xFF, 0xEF };
        var standard = AnyBase.CreateRfc4648Base64();
        var urlSafe = AnyBase.CreateRfc4648Base64Url();

        Assert.Multiple(() =>
        {
            Assert.That(standard.EncodeToString(bytes), Is.EqualTo("+//v"));
            Assert.That(urlSafe.EncodeToString(bytes), Is.EqualTo("-__v"));
            Assert.That(urlSafe.DecodeToBytes("-__v"), Is.EqualTo(bytes));
        });
    }

    [Test]
    public void PackedSpanApis_ReportExactLengthsAndRoundTrip()
    {
        var sut = AnyBase.CreateRfc4648Base64();
        var source = new byte[] { 0x66 };
        var symbols = new char[sut.GetEncodedLength(source.Length)];

        var symbolsWritten = sut.Encode(source.AsSpan(), symbols.AsSpan());
        var decoded = new byte[sut.GetDecodedLength(symbols.AsSpan())];
        var bytesWritten = sut.Decode(symbols.AsSpan(), decoded.AsSpan());

        Assert.Multiple(() =>
        {
            Assert.That(symbolsWritten, Is.EqualTo(4));
            Assert.That(new string(symbols), Is.EqualTo("Zg=="));
            Assert.That(sut.GetDecodedLength(symbols.Length), Is.EqualTo(3));
            Assert.That(decoded, Is.EqualTo(source));
            Assert.That(bytesWritten, Is.EqualTo(1));
        });
    }

    [Test]
    public void PackedMode_RequiresPowerOfTwoAlphabetAndRejectsSeparators()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => AnyBase.Create("0123456789", EncodingMode.Packed),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => AnyBase.CreateRfc4648Base64().EncodeToString("f", "-"),
                Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public void PackedStreams_RoundTripAcrossSingleByteChunks()
    {
        var sut = AnyBase.CreateRfc4648Base64();
        var source = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        using var input = new MemoryStream(source);
        using var encoded = new MemoryStream();

        sut.Encode(input, encoded, bufferSize: 1);

        Assert.That(Encoding.ASCII.GetString(encoded.ToArray()), Is.EqualTo(Convert.ToBase64String(source)));
        encoded.Position = 0;
        using var decoded = new MemoryStream();
        sut.Decode(encoded, decoded, bufferSize: 1);
        Assert.That(decoded.ToArray(), Is.EqualTo(source));
    }

    [Test]
    public async Task PackedStreamsAsync_RoundTripBase32WithoutPadding()
    {
        var sut = AnyBase.CreateRfc4648Base32(usePadding: false);
        var source = Encoding.UTF8.GetBytes("zero iniziale: \0 e onda 🌊");
        await using var input = new MemoryStream(source);
        await using var encoded = new MemoryStream();

        await sut.EncodeAsync(input, encoded, bufferSize: 2);
        encoded.Position = 0;
        await using var decoded = new MemoryStream();
        await sut.DecodeAsync(encoded, decoded, bufferSize: 1);

        Assert.That(decoded.ToArray(), Is.EqualTo(source));
    }
}
