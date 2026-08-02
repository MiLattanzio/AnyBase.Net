using System.Text;

namespace AnyBase.Net.Test;

public class BufferAndStreamTest
{
    [Test]
    public void SizingMethods_ReportExactSymbolAndByteCounts()
    {
        var sut = new Base<char>("0123456789ABCDEF");

        Assert.Multiple(() =>
        {
            Assert.That(sut.GetEncodedLength(3), Is.EqualTo(6));
            Assert.That(sut.GetDecodedLength(6), Is.EqualTo(3));
            Assert.That(sut.TryGetDecodedLength(6, out var bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(3));
            Assert.That(sut.TryGetDecodedLength(5, out var invalidBytes), Is.False);
            Assert.That(invalidBytes, Is.Zero);
            Assert.That(() => sut.GetDecodedLength(5), Throws.TypeOf<FormatException>());
            Assert.That(() => sut.GetEncodedLength(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void TextSizingMethods_AccountForVariableTokensAndSeparators()
    {
        var sut = new Base<string>(new[] { "z", "one" });
        var bytes = new byte[] { 65 };

        Assert.Multiple(() =>
        {
            Assert.That(sut.GetEncodedTextLength(bytes, "|"), Is.EqualTo(19));
            Assert.That(sut.GetMaxEncodedTextLength(1, "|"), Is.EqualTo(31));
            Assert.That(sut.EncodeToString(bytes, "|").Length, Is.EqualTo(sut.GetEncodedTextLength(bytes, "|")));
        });
    }

    [Test]
    public void SpanApis_RoundTripWithoutAllocatingDestination()
    {
        var sut = new Base<char>("0123456789ABCDEF");
        var source = new byte[] { 0, 1, 127, 128, 255 };
        var symbols = new char[sut.GetEncodedLength(source.Length)];
        var decoded = new byte[sut.GetDecodedLength(symbols.Length)];

        var symbolsWritten = sut.Encode(source.AsSpan(), symbols.AsSpan());
        var bytesWritten = sut.Decode(symbols.AsSpan(), decoded.AsSpan());

        Assert.Multiple(() =>
        {
            Assert.That(symbolsWritten, Is.EqualTo(symbols.Length));
            Assert.That(bytesWritten, Is.EqualTo(source.Length));
            Assert.That(decoded, Is.EqualTo(source));
            Assert.That(
                () => sut.Encode(source.AsSpan(), new char[symbols.Length - 1].AsSpan()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => sut.Decode(symbols.AsSpan(), new byte[source.Length - 1].AsSpan()),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void MemoryApis_RoundTripIntoCallerOwnedBuffers()
    {
        var sut = new Base<char>("01");
        ReadOnlyMemory<byte> source = new byte[] { 0, 65, 255 };
        var symbols = new char[sut.GetEncodedLength(source.Length)];
        var decoded = new byte[source.Length];

        var symbolsWritten = sut.EncodeMemory(source, symbols.AsMemory());
        var bytesWritten = sut.DecodeMemory(symbols.AsMemory(0, symbolsWritten), decoded.AsMemory());

        Assert.Multiple(() =>
        {
            Assert.That(symbolsWritten, Is.EqualTo(24));
            Assert.That(bytesWritten, Is.EqualTo(3));
            Assert.That(decoded, Is.EqualTo(source.ToArray()));
        });
    }

    [Test]
    public void StreamApis_RoundTripAcrossSingleByteChunksAndLeaveStreamsOpen()
    {
        var sut = new Base<char>("0123456789ABCDEF");
        var source = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        using var input = new MemoryStream(source);
        using var encoded = new MemoryStream();

        sut.Encode(input, encoded, bufferSize: 1);
        encoded.Position = 0;
        using var decoded = new MemoryStream();
        sut.Decode(encoded, decoded, bufferSize: 1);

        Assert.Multiple(() =>
        {
            Assert.That(decoded.ToArray(), Is.EqualTo(source));
            Assert.That(input.CanRead, Is.True);
            Assert.That(encoded.CanRead, Is.True);
            Assert.That(decoded.CanWrite, Is.True);
        });
    }

    [Test]
    public async Task StreamApisAsync_RoundTripPrefixAlphabetWithMulticharacterSeparator()
    {
        var sut = new Base<string>(new[] { "a", "ab" });
        var source = Encoding.UTF8.GetBytes("Stream 🌊");
        await using var input = new MemoryStream(source);
        await using var encoded = new MemoryStream();

        await sut.EncodeAsync(input, encoded, "::", bufferSize: 2, CancellationToken.None);
        encoded.Position = 0;
        await using var decoded = new MemoryStream();
        await sut.DecodeAsync(encoded, decoded, "::", bufferSize: 1, CancellationToken.None);

        Assert.That(decoded.ToArray(), Is.EqualTo(source));
    }

    [Test]
    public void StreamDecode_ReportsIncompleteByteGroup()
    {
        var sut = new Base<char>("0123456789ABCDEF");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("4"));
        using var output = new MemoryStream();

        Assert.That(
            () => sut.Decode(input, output, bufferSize: 1),
            Throws.TypeOf<FormatException>().With.Message.Contains("Incomplete byte group 0"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StreamApisAsync_HonorPreCanceledToken(bool encode)
    {
        var sut = new Base<char>("01");
        using var input = new MemoryStream(encode ? new byte[] { 1 } : "00000001"u8.ToArray());
        using var output = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(
            async () =>
            {
                if (encode)
                {
                    await sut.EncodeAsync(input, output, cancellationToken: cancellation.Token);
                }
                else
                {
                    await sut.DecodeAsync(input, output, cancellationToken: cancellation.Token);
                }
            },
            Throws.TypeOf<OperationCanceledException>());
    }
}
