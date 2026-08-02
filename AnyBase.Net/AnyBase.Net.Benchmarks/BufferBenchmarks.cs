using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace AnyBase.Net.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 5)]
public class BufferBenchmarks
{
    private Base<char> _codec = null!;
    private byte[] _source = null!;
    private char[] _encoded = null!;
    private char[] _symbolDestination = null!;
    private byte[] _byteDestination = null!;

    [Params(1024, 65536)]
    public int ByteCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _codec = new Base<char>(AnyBaseAlphabets.Hexadecimal);
        _source = new byte[ByteCount];
        new Random(42).NextBytes(_source);
        _encoded = _codec.Encode(_source);
        _symbolDestination = new char[_codec.GetEncodedLength(_source.Length)];
        _byteDestination = new byte[_codec.GetDecodedLength(_encoded.Length)];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Encode")]
    public char[] EncodeArray()
    {
        return _codec.Encode(_source);
    }

    [Benchmark]
    [BenchmarkCategory("Encode")]
    public int EncodeSpan()
    {
        return _codec.Encode(_source.AsSpan(), _symbolDestination.AsSpan());
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Decode")]
    public byte[] DecodeArray()
    {
        return _codec.DecodeToBytes(_encoded);
    }

    [Benchmark]
    [BenchmarkCategory("Decode")]
    public int DecodeSpan()
    {
        return _codec.Decode(_encoded.AsSpan(), _byteDestination.AsSpan());
    }
}
