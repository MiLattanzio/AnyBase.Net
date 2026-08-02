using System.Globalization;
using System.Text;

namespace AnyBase.Net.Tool;

/// <summary>
/// Parses command-line arguments and runs AnyBase.Net transformations.
/// </summary>
public static class CliApplication
{
    private const int StreamBufferSize = 81920;
    private const string DefaultAlphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// Runs the command-line application using binary standard streams.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="standardInput">The redirected input stream, or <see langword="null"/> when unavailable.</param>
    /// <param name="standardOutput">The binary-safe standard output stream.</param>
    /// <param name="standardError">The destination for textual diagnostics.</param>
    /// <param name="cancellationToken">A token used to cancel file and stream operations.</param>
    /// <returns>Zero on success, or two for invalid input and operational errors.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="args"/>, <paramref name="standardOutput"/>, or
    /// <paramref name="standardError"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public static async Task<int> RunAsync(
        string[] args,
        Stream? standardInput,
        Stream standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (!standardOutput.CanWrite)
        {
            throw new ArgumentException("Standard output stream must be writable.", nameof(standardOutput));
        }

        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            await WriteUtf8Async(standardOutput, HelpText, appendNewline: true, cancellationToken);
            return 0;
        }

        if (args is ["--version"])
        {
            var version = typeof(CliApplication).Assembly.GetName().Version?.ToString(3) ?? "unknown";
            await WriteUtf8Async(standardOutput, version, appendNewline: true, cancellationToken);
            return 0;
        }

        try
        {
            var options = Parse(args);
            if (options.HelpRequested)
            {
                await WriteUtf8Async(
                    standardOutput,
                    CommandHelp(options.Command),
                    appendNewline: true,
                    cancellationToken);
                return 0;
            }

            var alphabet = ResolveAlphabet(options);
            var codec = new Base<char>(alphabet);
            await using var input = await ResolveInputAsync(options, standardInput, cancellationToken);

            if (options.OutputFormat == DataFormat.Binary)
            {
                await using var output = OpenOutput(options, standardOutput);
                await TransformAsync(codec, options, input.Stream, output.Stream, cancellationToken);
                await output.Stream.FlushAsync(cancellationToken);
            }
            else
            {
                using var transformed = new MemoryStream();
                await TransformAsync(codec, options, input.Stream, transformed, cancellationToken);
                await WriteFormattedOutputAsync(
                    options,
                    transformed.ToArray(),
                    standardOutput,
                    cancellationToken);
            }

            return 0;
        }
        catch (Exception exception) when (exception is
            CliException or
            ArgumentException or
            FormatException or
            DecoderFallbackException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"error: {exception.Message}");
            return 2;
        }
    }

    /// <summary>
    /// Runs the CLI through text adapters retained for compatibility with existing hosts and tests.
    /// </summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextReader? standardInput,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        MemoryStream? input = null;
        if (standardInput != null)
        {
            var text = await standardInput.ReadToEndAsync(cancellationToken);
            input = new MemoryStream(StrictUtf8.GetBytes(text), writable: false);
        }

        using (input)
        using (var output = new MemoryStream())
        {
            var exitCode = await RunAsync(args, input, output, standardError, cancellationToken);
            if (output.Length > 0)
            {
                var text = StrictUtf8.GetString(output.ToArray());
                await standardOutput.WriteAsync(text);
            }

            return exitCode;
        }
    }

    private static CliOptions Parse(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        if (command is not ("encode" or "decode"))
        {
            throw new CliException($"Unknown command '{args[0]}'. Use --help for usage.");
        }

        string? value = null;
        string? alphabet = null;
        int? numberBase = null;
        string? inputPath = null;
        string? outputPath = null;
        string? separator = null;
        var inputFormat = DataFormat.Text;
        var outputFormat = DataFormat.Text;
        var helpRequested = false;

        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-h":
                case "--help":
                    helpRequested = true;
                    break;
                case "-a":
                case "--alphabet":
                    alphabet = ReadOptionValue(args, ref index);
                    break;
                case "-b":
                case "--base":
                    var rawBase = ReadOptionValue(args, ref index);
                    if (!int.TryParse(rawBase, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBase))
                    {
                        throw new CliException($"'{rawBase}' is not a valid base.");
                    }

                    numberBase = parsedBase;
                    break;
                case "-i":
                case "--input":
                    inputPath = ReadOptionValue(args, ref index);
                    break;
                case "-o":
                case "--output":
                    outputPath = ReadOptionValue(args, ref index);
                    break;
                case "-s":
                case "--separator":
                    separator = ReadOptionValue(args, ref index);
                    break;
                case "--input-format":
                    inputFormat = ParseFormat(ReadOptionValue(args, ref index), "--input-format");
                    break;
                case "--output-format":
                    outputFormat = ParseFormat(ReadOptionValue(args, ref index), "--output-format");
                    break;
                default:
                    if (args[index].StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new CliException($"Unknown option '{args[index]}'.");
                    }

                    if (value != null)
                    {
                        throw new CliException("Only one positional value is allowed. Quote values containing spaces.");
                    }

                    value = args[index];
                    break;
            }
        }

        if (alphabet != null && numberBase != null)
        {
            throw new CliException("Use either --alphabet or --base, not both.");
        }

        if (value != null && inputPath != null)
        {
            throw new CliException("A positional value cannot be combined with --input.");
        }

        if (value != null && inputFormat == DataFormat.Binary)
        {
            throw new CliException("Binary input must come from --input FILE or redirected standard input.");
        }

        return new CliOptions(
            command,
            value,
            alphabet,
            numberBase,
            inputPath,
            outputPath,
            separator,
            inputFormat,
            outputFormat,
            helpRequested);
    }

    private static DataFormat ParseFormat(string value, string option)
    {
        return value.ToLowerInvariant() switch
        {
            "text" => DataFormat.Text,
            "binary" => DataFormat.Binary,
            "hex" => DataFormat.Hex,
            _ => throw new CliException($"{option} must be text, binary, or hex; found '{value}'.")
        };
    }

    private static string ReadOptionValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new CliException($"Option '{args[index - 1]}' requires a value.");
        }

        return args[index];
    }

    private static string ResolveAlphabet(CliOptions options)
    {
        if (options.Alphabet != null)
        {
            var alphabet = AnyBaseAlphabets.TryGet(options.Alphabet, out var preset)
                ? preset
                : options.Alphabet;
            ValidateAlphabet(alphabet, options.Separator);
            return alphabet;
        }

        var numberBase = options.NumberBase ?? 16;
        if (numberBase is < 2 or > 64)
        {
            throw new CliException("--base must be between 2 and 64.");
        }

        var resolved = DefaultAlphabet[..numberBase];
        ValidateAlphabet(resolved, options.Separator);
        return resolved;
    }

    private static void ValidateAlphabet(string alphabet, string? separator)
    {
        var validation = separator == null
            ? AlphabetValidator.Validate(alphabet)
            : AlphabetValidator.ValidateWithSeparator(alphabet, separator);
        var diagnostic = validation.Diagnostics.FirstOrDefault();
        if (diagnostic != null)
        {
            throw new CliException(diagnostic.Message);
        }
    }

    private static async Task<StreamHandle> ResolveInputAsync(
        CliOptions options,
        Stream? standardInput,
        CancellationToken cancellationToken)
    {
        if (options.Value != null)
        {
            var bytes = options.InputFormat switch
            {
                DataFormat.Text => StrictUtf8.GetBytes(options.Value),
                DataFormat.Hex => ParseHex(options.Value),
                _ => throw new CliException("Binary input cannot be supplied as a positional value.")
            };
            return StreamHandle.Owned(new MemoryStream(bytes, writable: false));
        }

        if (options.InputPath is { } inputPath && inputPath != "-")
        {
            if (options.InputFormat == DataFormat.Binary)
            {
                return StreamHandle.Owned(new FileStream(
                    inputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    StreamBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan));
            }

            var fileBytes = await File.ReadAllBytesAsync(inputPath, cancellationToken);
            return StreamHandle.Owned(new MemoryStream(ConvertInput(fileBytes, options.InputFormat), writable: false));
        }

        if (standardInput == null)
        {
            throw new CliException("Provide a value, --input FILE, or redirected standard input.");
        }

        if (options.InputFormat == DataFormat.Binary)
        {
            return StreamHandle.Borrowed(standardInput);
        }

        var redirectedBytes = await ReadAllBytesAsync(standardInput, cancellationToken);
        return StreamHandle.Owned(
            new MemoryStream(ConvertInput(redirectedBytes, options.InputFormat), writable: false));
    }

    private static byte[] ConvertInput(byte[] source, DataFormat format)
    {
        if (format == DataFormat.Text)
        {
            StrictUtf8.GetString(source);
            return source;
        }

        if (format == DataFormat.Hex)
        {
            return ParseHex(StrictUtf8.GetString(source));
        }

        return source;
    }

    private static byte[] ParseHex(string value)
    {
        var compact = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (compact.Length % 2 != 0)
        {
            throw new CliException($"Hex input must contain an even number of digits; found {compact.Length}.");
        }

        try
        {
            return Convert.FromHexString(compact);
        }
        catch (FormatException exception)
        {
            throw new CliException($"Hex input is invalid: {exception.Message}");
        }
    }

    private static StreamHandle OpenOutput(CliOptions options, Stream standardOutput)
    {
        if (options.OutputPath == null || options.OutputPath == "-")
        {
            return StreamHandle.Borrowed(standardOutput);
        }

        return StreamHandle.Owned(new FileStream(
            options.OutputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    private static Task TransformAsync(
        Base<char> codec,
        CliOptions options,
        Stream input,
        Stream output,
        CancellationToken cancellationToken)
    {
        return options.Command switch
        {
            "encode" => codec.EncodeAsync(
                input,
                output,
                options.Separator,
                StreamBufferSize,
                cancellationToken),
            "decode" => codec.DecodeAsync(
                input,
                output,
                options.Separator,
                StreamBufferSize,
                cancellationToken),
            _ => throw new CliException($"Unknown command '{options.Command}'.")
        };
    }

    private static async Task WriteFormattedOutputAsync(
        CliOptions options,
        byte[] transformed,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        byte[] outputBytes;
        switch (options.OutputFormat)
        {
            case DataFormat.Text:
                StrictUtf8.GetString(transformed);
                outputBytes = transformed;
                break;
            case DataFormat.Hex:
                outputBytes = Encoding.ASCII.GetBytes(Convert.ToHexString(transformed));
                break;
            default:
                outputBytes = transformed;
                break;
        }

        await using var output = OpenOutput(options, standardOutput);
        await output.Stream.WriteAsync(outputBytes.AsMemory(), cancellationToken);
        if (output.IsBorrowed)
        {
            await output.Stream.WriteAsync("\n"u8.ToArray().AsMemory(), cancellationToken);
        }

        await output.Stream.FlushAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream input, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, StreamBufferSize, cancellationToken);
        return buffer.ToArray();
    }

    private static async Task WriteUtf8Async(
        Stream output,
        string value,
        bool appendNewline,
        CancellationToken cancellationToken)
    {
        var bytes = StrictUtf8.GetBytes(appendNewline ? value + Environment.NewLine : value);
        await output.WriteAsync(bytes.AsMemory(), cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static string CommandHelp(string command)
    {
        return $"""
            Usage: anybase {command} [VALUE] [options]

            Options:
              -b, --base <2..64>          Use the built-in ordered alphabet (default: 16).
              -a, --alphabet <value>      Use a preset name or custom ordered alphabet.
              -s, --separator <text>      Separate every encoded identity symbol.
              -i, --input <file|->        Read from a file or stdin.
              -o, --output <file|->       Write to a file or stdout.
                  --input-format <format> text, binary, or hex (default: text).
                  --output-format <format> text, binary, or hex (default: text).
              -h, --help                  Show command help.

            Binary output is written byte-for-byte without a trailing newline.
            Presets: binary, octal, decimal, hex, base32, base64, base64url.
            """;
    }

    private const string HelpText = """
        AnyBase.Net command-line tool

        Usage:
          anybase encode [VALUE] [options]
          anybase decode [VALUE] [options]
          anybase --version

        Examples:
          anybase encode "Hello" --alphabet hex
          anybase decode 48656C6C6F --alphabet hex
          anybase encode --input photo.bin --input-format binary --output encoded.txt
          anybase decode --input encoded.txt --output photo.bin --output-format binary
          anybase encode "41 42" --input-format hex --output-format binary

        Formats: text, binary, hex.
        Alphabet presets: binary, octal, decimal, hex, base32, base64, base64url.

        Run 'anybase <command> --help' for command options.
        """;

    private enum DataFormat
    {
        Text,
        Binary,
        Hex
    }

    private sealed record CliOptions(
        string Command,
        string? Value,
        string? Alphabet,
        int? NumberBase,
        string? InputPath,
        string? OutputPath,
        string? Separator,
        DataFormat InputFormat,
        DataFormat OutputFormat,
        bool HelpRequested);

    private sealed class StreamHandle : IAsyncDisposable
    {
        private readonly bool _ownsStream;

        private StreamHandle(Stream stream, bool ownsStream)
        {
            Stream = stream;
            _ownsStream = ownsStream;
        }

        public Stream Stream { get; }

        public bool IsBorrowed => !_ownsStream;

        public static StreamHandle Borrowed(Stream stream) => new StreamHandle(stream, ownsStream: false);

        public static StreamHandle Owned(Stream stream) => new StreamHandle(stream, ownsStream: true);

        public async ValueTask DisposeAsync()
        {
            if (_ownsStream)
            {
                await Stream.DisposeAsync();
            }
        }
    }

    private sealed class CliException : Exception
    {
        public CliException(string message)
            : base(message)
        {
        }
    }
}
