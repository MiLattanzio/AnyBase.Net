using System.Globalization;
using System.Text;

namespace AnyBase.Net.Tool;

/// <summary>
/// Parses command-line arguments and runs AnyBase.Net text transformations.
/// </summary>
public static class CliApplication
{
    private const string DefaultAlphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";

    /// <summary>
    /// Runs the command-line application using the supplied streams.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="standardInput">The redirected input, or <see langword="null"/> when unavailable.</param>
    /// <param name="standardOutput">The destination for successful output.</param>
    /// <param name="standardError">The destination for diagnostics.</param>
    /// <param name="cancellationToken">A token used to cancel file and stream operations.</param>
    /// <returns>Zero on success, or two for invalid input and operational errors.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="args"/>, <paramref name="standardOutput"/>, or
    /// <paramref name="standardError"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
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

        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            await standardOutput.WriteLineAsync(HelpText);
            return 0;
        }

        if (args is ["--version"])
        {
            var version = typeof(CliApplication).Assembly.GetName().Version?.ToString(3) ?? "unknown";
            await standardOutput.WriteLineAsync(version);
            return 0;
        }

        try
        {
            var options = Parse(args);
            if (options.HelpRequested)
            {
                await standardOutput.WriteLineAsync(CommandHelp(options.Command));
                return 0;
            }

            var alphabet = ResolveAlphabet(options);
            var input = await ResolveInputAsync(options, standardInput, cancellationToken);
            var encoder = new Base<char>(alphabet);
            var result = options.Command switch
            {
                "encode" => encoder.EncodeToString(input),
                "decode" => encoder.DecodeToString(input),
                _ => throw new CliException($"Unknown command '{options.Command}'.")
            };

            if (options.OutputPath == null || options.OutputPath == "-")
            {
                await standardOutput.WriteLineAsync(result);
            }
            else
            {
                await File.WriteAllTextAsync(options.OutputPath, result, new UTF8Encoding(false), cancellationToken);
            }

            return 0;
        }
        catch (Exception exception) when (exception is
            CliException or
            ArgumentException or
            FormatException or
            DecoderFallbackException or
            IOException or
            UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"error: {exception.Message}");
            return 2;
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

        return new CliOptions(command, value, alphabet, numberBase, inputPath, outputPath, helpRequested);
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
            if (options.Alphabet.Length < 2)
            {
                throw new CliException("The alphabet must contain at least two symbols.");
            }

            if (options.Alphabet.Distinct().Count() != options.Alphabet.Length)
            {
                throw new CliException("The alphabet must contain unique symbols.");
            }

            return options.Alphabet;
        }

        var numberBase = options.NumberBase ?? 16;
        if (numberBase is < 2 or > 64)
        {
            throw new CliException("--base must be between 2 and 64.");
        }

        return DefaultAlphabet[..numberBase];
    }

    private static async Task<string> ResolveInputAsync(
        CliOptions options,
        TextReader? standardInput,
        CancellationToken cancellationToken)
    {
        if (options.Value != null)
        {
            return options.Value;
        }

        if (options.InputPath is { } inputPath && inputPath != "-")
        {
            return await File.ReadAllTextAsync(inputPath, cancellationToken);
        }

        if (standardInput == null)
        {
            throw new CliException("Provide a value, --input FILE, or redirected standard input.");
        }

        return await standardInput.ReadToEndAsync(cancellationToken);
    }

    private static string CommandHelp(string command)
    {
        return $"""
            Usage: anybase {command} [VALUE] [options]

            Options:
              -b, --base <2..64>       Use the built-in ordered alphabet (default: 16).
              -a, --alphabet <symbols> Use a custom ordered alphabet.
              -i, --input <file|->     Read the value from a UTF-8 file or stdin.
              -o, --output <file|->    Write the result to a UTF-8 file or stdout.
              -h, --help               Show command help.
            """;
    }

    private const string HelpText = """
        AnyBase.Net command-line tool

        Usage:
          anybase encode [VALUE] [options]
          anybase decode [VALUE] [options]
          anybase --version

        Examples:
          anybase encode "Hello" --base 16
          anybase decode 48656C6C6F --base 16
          anybase encode "Hello" --alphabet "01"
          echo -n "Hello" | anybase encode --base 64

        Run 'anybase <command> --help' for command options.
        """;

    private sealed record CliOptions(
        string Command,
        string? Value,
        string? Alphabet,
        int? NumberBase,
        string? InputPath,
        string? OutputPath,
        bool HelpRequested);

    private sealed class CliException : Exception
    {
        public CliException(string message)
            : base(message)
        {
        }
    }
}
