using AnyBase.Net.Tool;

namespace AnyBase.Net.Test;

public class CliApplicationTest
{
    [TestCase("encode", "A", "16", "41")]
    [TestCase("encode", "A", "2", "01000001")]
    [TestCase("decode", "41", "16", "A")]
    public async Task RunAsync_TransformsValue(string command, string value, string numberBase, string expected)
    {
        var result = await RunAsync(command, value, "--base", numberBase);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Output.TrimEnd(), Is.EqualTo(expected));
            Assert.That(result.Error, Is.Empty);
        });
    }

    [Test]
    public async Task RunAsync_UsesCustomAlphabet()
    {
        var result = await RunAsync("encode", "A", "--alphabet", "ZA");

        Assert.That(result.Output.TrimEnd(), Is.EqualTo("ZAZZZZZA"));
    }

    [Test]
    public async Task RunAsync_ReadsRedirectedInput()
    {
        var result = await RunAsync(new StringReader("A"), "encode", "--base", "16");

        Assert.That(result.Output.TrimEnd(), Is.EqualTo("41"));
    }

    [Test]
    public async Task RunAsync_ReadsAndWritesUtf8Files()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"anybase-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var inputPath = Path.Combine(directory, "input.txt");
        var outputPath = Path.Combine(directory, "output.txt");

        try
        {
            await File.WriteAllTextAsync(inputPath, "A");

            var result = await RunAsync(
                "encode",
                "--input", inputPath,
                "--output", outputPath,
                "--base", "16");

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.Zero);
                Assert.That(result.Output, Is.Empty);
                Assert.That(result.Error, Is.Empty);
                Assert.That(File.ReadAllText(outputPath), Is.EqualTo("41"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase("unknown")]
    [TestCase("encode")]
    [TestCase("encode", "A", "--base", "1")]
    [TestCase("encode", "A", "--alphabet", "00")]
    [TestCase("encode", "A", "--base", "16", "--alphabet", "01")]
    [TestCase("decode", "4Z", "--base", "16")]
    public async Task RunAsync_InvalidInput_ReturnsUsageError(params string[] args)
    {
        var result = await RunAsync(args);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Output, Is.Empty);
            Assert.That(result.Error, Does.StartWith("error: "));
        });
    }

    [Test]
    public async Task RunAsync_Help_ReturnsSuccess()
    {
        var result = await RunAsync("--help");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Output, Does.Contain("anybase encode"));
            Assert.That(result.Error, Is.Empty);
        });
    }

    private static Task<CliResult> RunAsync(params string[] args)
    {
        return RunAsync(null, args);
    }

    private static async Task<CliResult> RunAsync(TextReader? input, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await CliApplication.RunAsync(args, input, output, error);
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliResult(int ExitCode, string Output, string Error);
}
