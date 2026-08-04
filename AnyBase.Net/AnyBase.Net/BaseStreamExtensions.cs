using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnyBase.Net
{
    /// <summary>
    /// Provides bounded-memory stream encoding and decoding for AnyBase.Net codecs.
    /// </summary>
    /// <remarks>
    /// Encoded symbols are serialized as strict UTF-8 text. Source and destination
    /// streams remain open after every operation.
    /// </remarks>
    public static class BaseStreamExtensions
    {
        private const int DefaultBufferSize = 81920;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// Incrementally encodes bytes from one stream as UTF-8 identity symbol text in another stream.
        /// </summary>
        /// <param name="codec">The codec whose identity symbols are written.</param>
        /// <param name="input">The readable stream containing raw bytes.</param>
        /// <param name="output">The writable stream that receives encoded UTF-8 symbol text.</param>
        /// <param name="separator">An optional separator written between symbols.</param>
        /// <param name="bufferSize">The positive number of input bytes processed per chunk.</param>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">A stream is unusable or the text configuration is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is not positive.</exception>
        public static void Encode<TBase>(
            this Base<TBase> codec,
            Stream input,
            Stream output,
            string? separator = null,
            int bufferSize = DefaultBufferSize)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            ValidateStreams(codec, input, output, bufferSize);
            var tokens = GetTokens(codec, separator);
            if (codec.Mode == EncodingMode.Packed)
            {
                EncodePacked(codec, input, output, tokens, bufferSize);
                return;
            }

            var inputBuffer = new byte[bufferSize];
            var symbolBuffer = new TBase[codec.GetEncodedLength(bufferSize)];
            var hasWrittenSymbol = false;

            while (true)
            {
                var bytesRead = input.Read(inputBuffer, 0, inputBuffer.Length);
                if (bytesRead == 0)
                {
                    return;
                }

                var symbolsWritten = codec.Encode(
                    inputBuffer.AsSpan(0, bytesRead),
                    symbolBuffer.AsSpan());
                var text = BuildEncodedText(
                    codec,
                    symbolBuffer,
                    symbolsWritten,
                    tokens,
                    separator,
                    ref hasWrittenSymbol);
                var encodedBytes = StrictUtf8.GetBytes(text);
                output.Write(encodedBytes, 0, encodedBytes.Length);
            }
        }

        /// <summary>
        /// Incrementally encodes bytes from one stream as UTF-8 identity symbol text in another stream.
        /// </summary>
        /// <param name="codec">The codec whose identity symbols are written.</param>
        /// <param name="input">The readable stream containing raw bytes.</param>
        /// <param name="output">The writable stream that receives encoded UTF-8 symbol text.</param>
        /// <param name="separator">An optional separator written between symbols.</param>
        /// <param name="bufferSize">The positive number of input bytes processed per chunk.</param>
        /// <param name="cancellationToken">The token used to cancel asynchronous I/O.</param>
        /// <returns>A task that represents the complete encoding operation.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
        public static async Task EncodeAsync<TBase>(
            this Base<TBase> codec,
            Stream input,
            Stream output,
            string? separator = null,
            int bufferSize = DefaultBufferSize,
            CancellationToken cancellationToken = default)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            ValidateStreams(codec, input, output, bufferSize);
            cancellationToken.ThrowIfCancellationRequested();
            var tokens = GetTokens(codec, separator);
            if (codec.Mode == EncodingMode.Packed)
            {
                await EncodePackedAsync(codec, input, output, tokens, bufferSize, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var inputBuffer = new byte[bufferSize];
            var symbolBuffer = new TBase[codec.GetEncodedLength(bufferSize)];
            var hasWrittenSymbol = false;

            while (true)
            {
                var bytesRead = await input
                    .ReadAsync(inputBuffer.AsMemory(0, inputBuffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    return;
                }

                var symbolsWritten = codec.EncodeMemory(
                    inputBuffer.AsMemory(0, bytesRead),
                    symbolBuffer.AsMemory());
                var text = BuildEncodedText(
                    codec,
                    symbolBuffer,
                    symbolsWritten,
                    tokens,
                    separator,
                    ref hasWrittenSymbol);
                var encodedBytes = StrictUtf8.GetBytes(text);
                await output
                    .WriteAsync(encodedBytes.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Incrementally decodes UTF-8 identity symbol text into raw bytes.
        /// </summary>
        /// <param name="codec">The codec whose identity symbols are read.</param>
        /// <param name="input">The readable stream containing strict UTF-8 symbol text.</param>
        /// <param name="output">The writable stream that receives decoded raw bytes.</param>
        /// <param name="separator">The separator expected between symbols, if any.</param>
        /// <param name="bufferSize">The positive number of characters and output bytes buffered per chunk.</param>
        /// <exception cref="FormatException">The input contains an invalid symbol, byte group, or separator.</exception>
        /// <exception cref="DecoderFallbackException">The input is not valid UTF-8.</exception>
        public static void Decode<TBase>(
            this Base<TBase> codec,
            Stream input,
            Stream output,
            string? separator = null,
            int bufferSize = DefaultBufferSize)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            ValidateStreams(codec, input, output, bufferSize);
            var tokens = GetTokens(codec, separator);
            var decoder = new IncrementalTextDecoder<TBase>(codec, tokens, separator);
            var characterBuffer = new char[bufferSize];
            var outputBuffer = new byte[bufferSize];
            var outputCount = 0;

            using (var reader = new StreamReader(
                       input,
                       StrictUtf8,
                       detectEncodingFromByteOrderMarks: false,
                       bufferSize,
                       leaveOpen: true))
            {
                while (true)
                {
                    var charactersRead = reader.Read(characterBuffer, 0, characterBuffer.Length);
                    if (charactersRead == 0)
                    {
                        break;
                    }

                    for (var index = 0; index < charactersRead; index++)
                    {
                        if (decoder.Consume(characterBuffer[index], out var decodedByte))
                        {
                            outputBuffer[outputCount++] = decodedByte;
                            if (outputCount == outputBuffer.Length)
                            {
                                output.Write(outputBuffer, 0, outputCount);
                                outputCount = 0;
                            }
                        }
                    }
                }
            }

            if (decoder.Complete(out var finalByte))
            {
                outputBuffer[outputCount++] = finalByte;
            }

            if (outputCount > 0)
            {
                output.Write(outputBuffer, 0, outputCount);
            }
        }

        /// <summary>
        /// Incrementally decodes UTF-8 identity symbol text into raw bytes.
        /// </summary>
        /// <param name="codec">The codec whose identity symbols are read.</param>
        /// <param name="input">The readable stream containing strict UTF-8 symbol text.</param>
        /// <param name="output">The writable stream that receives decoded raw bytes.</param>
        /// <param name="separator">The separator expected between symbols, if any.</param>
        /// <param name="bufferSize">The positive number of characters and output bytes buffered per chunk.</param>
        /// <param name="cancellationToken">The token used to cancel asynchronous I/O.</param>
        /// <returns>A task that represents the complete decoding operation.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
        /// <exception cref="FormatException">The input contains an invalid symbol, byte group, or separator.</exception>
        /// <exception cref="DecoderFallbackException">The input is not valid UTF-8.</exception>
        public static async Task DecodeAsync<TBase>(
            this Base<TBase> codec,
            Stream input,
            Stream output,
            string? separator = null,
            int bufferSize = DefaultBufferSize,
            CancellationToken cancellationToken = default)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            ValidateStreams(codec, input, output, bufferSize);
            cancellationToken.ThrowIfCancellationRequested();
            var tokens = GetTokens(codec, separator);
            var decoder = new IncrementalTextDecoder<TBase>(codec, tokens, separator);
            var characterBuffer = new char[bufferSize];
            var outputBuffer = new byte[bufferSize];
            var outputCount = 0;

            using (var reader = new StreamReader(
                       input,
                       StrictUtf8,
                       detectEncodingFromByteOrderMarks: false,
                       bufferSize,
                       leaveOpen: true))
            {
                while (true)
                {
                    var charactersRead = await reader
                        .ReadAsync(characterBuffer.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    if (charactersRead == 0)
                    {
                        break;
                    }

                    for (var index = 0; index < charactersRead; index++)
                    {
                        if (decoder.Consume(characterBuffer[index], out var decodedByte))
                        {
                            outputBuffer[outputCount++] = decodedByte;
                            if (outputCount == outputBuffer.Length)
                            {
                                await output
                                    .WriteAsync(outputBuffer.AsMemory(0, outputCount), cancellationToken)
                                    .ConfigureAwait(false);
                                outputCount = 0;
                            }
                        }
                    }
                }
            }

            if (decoder.Complete(out var finalByte))
            {
                outputBuffer[outputCount++] = finalByte;
            }

            if (outputCount > 0)
            {
                await output
                    .WriteAsync(outputBuffer.AsMemory(0, outputCount), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static void EncodePacked<TBase>(
            Base<TBase> codec,
            Stream input,
            Stream output,
            IReadOnlyList<TextToken> tokens,
            int bufferSize)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            var quantumBytes = codec.PackedInputQuantumBytes;
            var inputBuffer = new byte[checked(bufferSize + quantumBytes - 1)];
            var symbolBuffer = new TBase[codec.GetEncodedLength(inputBuffer.Length)];
            var buffered = 0;
            var hasWrittenSymbol = false;

            while (true)
            {
                var bytesRead = input.Read(inputBuffer, buffered, bufferSize);
                if (bytesRead == 0)
                {
                    break;
                }

                var total = buffered + bytesRead;
                var processCount = total - total % quantumBytes;
                if (processCount > 0)
                {
                    WritePackedChunk(
                        codec,
                        inputBuffer.AsSpan(0, processCount),
                        output,
                        symbolBuffer,
                        tokens,
                        ref hasWrittenSymbol);
                }

                buffered = total - processCount;
                if (buffered > 0)
                {
                    Array.Copy(inputBuffer, processCount, inputBuffer, 0, buffered);
                }
            }

            if (buffered > 0)
            {
                WritePackedChunk(
                    codec,
                    inputBuffer.AsSpan(0, buffered),
                    output,
                    symbolBuffer,
                    tokens,
                    ref hasWrittenSymbol);
            }
        }

        private static async Task EncodePackedAsync<TBase>(
            Base<TBase> codec,
            Stream input,
            Stream output,
            IReadOnlyList<TextToken> tokens,
            int bufferSize,
            CancellationToken cancellationToken)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            var quantumBytes = codec.PackedInputQuantumBytes;
            var inputBuffer = new byte[checked(bufferSize + quantumBytes - 1)];
            var symbolBuffer = new TBase[codec.GetEncodedLength(inputBuffer.Length)];
            var buffered = 0;
            var hasWrittenSymbol = false;

            while (true)
            {
                var bytesRead = await input
                    .ReadAsync(inputBuffer.AsMemory(buffered, bufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                var total = buffered + bytesRead;
                var processCount = total - total % quantumBytes;
                if (processCount > 0)
                {
                    await WritePackedChunkAsync(
                            codec,
                            inputBuffer.AsMemory(0, processCount),
                            output,
                            symbolBuffer,
                            tokens,
                            hasWrittenSymbol,
                            cancellationToken)
                        .ConfigureAwait(false);
                    hasWrittenSymbol = true;
                }

                buffered = total - processCount;
                if (buffered > 0)
                {
                    Array.Copy(inputBuffer, processCount, inputBuffer, 0, buffered);
                }
            }

            if (buffered > 0)
            {
                await WritePackedChunkAsync(
                        codec,
                        inputBuffer.AsMemory(0, buffered),
                        output,
                        symbolBuffer,
                        tokens,
                        hasWrittenSymbol,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static void WritePackedChunk<TBase>(
            Base<TBase> codec,
            ReadOnlySpan<byte> bytes,
            Stream output,
            TBase[] symbolBuffer,
            IReadOnlyList<TextToken> tokens,
            ref bool hasWrittenSymbol)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            var symbolsWritten = codec.Encode(bytes, symbolBuffer.AsSpan());
            var text = BuildEncodedText(
                codec,
                symbolBuffer,
                symbolsWritten,
                tokens,
                null,
                ref hasWrittenSymbol);
            var encodedBytes = StrictUtf8.GetBytes(text);
            output.Write(encodedBytes, 0, encodedBytes.Length);
        }

        private static async Task WritePackedChunkAsync<TBase>(
            Base<TBase> codec,
            ReadOnlyMemory<byte> bytes,
            Stream output,
            TBase[] symbolBuffer,
            IReadOnlyList<TextToken> tokens,
            bool hasWrittenSymbol,
            CancellationToken cancellationToken)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            var symbolsWritten = codec.EncodeMemory(bytes, symbolBuffer.AsMemory());
            var text = BuildEncodedText(
                codec,
                symbolBuffer,
                symbolsWritten,
                tokens,
                null,
                ref hasWrittenSymbol);
            var encodedBytes = StrictUtf8.GetBytes(text);
            await output.WriteAsync(encodedBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        private static IReadOnlyList<TextToken> GetTokens<TBase>(Base<TBase> codec, string? separator)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            if (codec.Mode == EncodingMode.Packed && separator != null)
            {
                throw new InvalidOperationException(
                    "Packed mode does not support symbol separators because they are not part of RFC 4648 encodings.");
            }

            var validation = separator == null
                ? AlphabetValidator.Validate(codec.Identity, codec.Comparer)
                : AlphabetValidator.ValidateWithSeparator(codec.Identity, separator, codec.Comparer);
            var diagnostic = validation.Diagnostics.FirstOrDefault();
            if (diagnostic != null)
            {
                if (diagnostic.Kind == AlphabetValidationDiagnosticKind.EmptySeparator ||
                    diagnostic.Kind == AlphabetValidationDiagnosticKind.SeparatorCollision)
                {
                    throw new ArgumentException(diagnostic.Message, nameof(separator));
                }

                throw new InvalidOperationException(diagnostic.Message);
            }

            var tokens = codec.Identity
                .Select((symbol, index) => new TextToken(AlphabetValidator.SymbolText(symbol), index, separator))
                .ToArray();
            if (codec.Mode != EncodingMode.Packed || !codec.SupportsPadding)
            {
                return tokens;
            }

            var paddingText = AlphabetValidator.SymbolText(codec.PaddingSymbol);
            if (paddingText.Length == 0 || tokens.Any(token =>
                    token.Text.StartsWith(paddingText, StringComparison.Ordinal) ||
                    paddingText.StartsWith(token.Text, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "The padding symbol text must be non-empty and prefix-distinct from every alphabet symbol.");
            }

            return tokens
                .Concat(new[] { new TextToken(paddingText, -1, separator, isPadding: true) })
                .ToArray();
        }

        private static string BuildEncodedText<TBase>(
            Base<TBase> codec,
            TBase[] symbols,
            int symbolCount,
            IReadOnlyList<TextToken> tokens,
            string? separator,
            ref bool hasWrittenSymbol)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            var builder = new StringBuilder();
            for (var index = 0; index < symbolCount; index++)
            {
                if (separator != null && hasWrittenSymbol)
                {
                    builder.Append(separator);
                }

                if (codec.IsPaddingSymbol(symbols[index]))
                {
                    builder.Append(AlphabetValidator.SymbolText(codec.PaddingSymbol));
                }
                else
                {
                    var identityIndex = codec.GetIdentityIndex(symbols[index]);
                    builder.Append(tokens[identityIndex].Text);
                }
                hasWrittenSymbol = true;
            }

            return builder.ToString();
        }

        private static void ValidateStreams<TBase>(
            Base<TBase> codec,
            Stream input,
            Stream output,
            int bufferSize)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (ReferenceEquals(input, output))
            {
                throw new ArgumentException("Input and output streams must be different.", nameof(output));
            }

            if (!input.CanRead)
            {
                throw new ArgumentException("Input stream must be readable.", nameof(input));
            }

            if (!output.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable.", nameof(output));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Buffer size must be positive.");
            }

            codec.GetEncodedLength(bufferSize);
        }

        private sealed class IncrementalTextDecoder<TBase>
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            private readonly Base<TBase> _codec;
            private readonly IReadOnlyList<TextToken> _tokens;
            private readonly string? _separator;
            private readonly StringBuilder _pending = new StringBuilder();
            private int _byteGroup;
            private int _groupOffset;
            private int _groupValue;
            private int _symbolIndex;
            private int _textPosition;
            private int _packedBuffer;
            private int _packedBits;
            private int _packedDataSymbolCount;
            private int _packedPaddingCount;
            private bool _packedSawPadding;
            private bool _lastTokenEndedWithSeparator;

            public IncrementalTextDecoder(
                Base<TBase> codec,
                IReadOnlyList<TextToken> tokens,
                string? separator)
            {
                _codec = codec;
                _tokens = tokens;
                _separator = separator;
            }

            public bool Consume(char character, out byte decodedByte)
            {
                if (_pending.Length == 0 && _lastTokenEndedWithSeparator)
                {
                    _lastTokenEndedWithSeparator = false;
                }

                _pending.Append(character);
                _textPosition++;
                var pending = _pending.ToString();

                if (_separator == null)
                {
                    var exact = _tokens.FirstOrDefault(token =>
                        string.Equals(token.Text, pending, StringComparison.Ordinal));
                    if (exact != null)
                    {
                        _pending.Clear();
                        return AddToken(exact, out decodedByte);
                    }

                    if (_tokens.Any(token => token.Text.StartsWith(pending, StringComparison.Ordinal)))
                    {
                        decodedByte = 0;
                        return false;
                    }
                }
                else
                {
                    var exact = _tokens.FirstOrDefault(token =>
                        string.Equals(token.BoundaryText, pending, StringComparison.Ordinal));
                    if (exact != null)
                    {
                        _pending.Clear();
                        _lastTokenEndedWithSeparator = true;
                        return AddToken(exact, out decodedByte);
                    }

                    if (_tokens.Any(token => token.BoundaryText.StartsWith(pending, StringComparison.Ordinal)))
                    {
                        decodedByte = 0;
                        return false;
                    }
                }

                var segmentStart = _textPosition - _pending.Length;
                throw new FormatException(
                    $"Encoded stream has no identity symbol matching at text position {segmentStart}. " +
                    $"Pending input is '{AlphabetValidator.EscapeForMessage(pending)}'.");
            }

            public bool Complete(out byte decodedByte)
            {
                if (_separator != null)
                {
                    if (_pending.Length == 0 && _lastTokenEndedWithSeparator)
                    {
                        throw new FormatException(
                            $"Encoded stream ends with separator " +
                            $"'{AlphabetValidator.EscapeForMessage(_separator)}' at text position " +
                            $"{_textPosition - _separator.Length}.");
                    }

                    if (_pending.Length > 0)
                    {
                        var pending = _pending.ToString();
                        var exact = _tokens.FirstOrDefault(token =>
                            string.Equals(token.Text, pending, StringComparison.Ordinal));
                        if (exact == null)
                        {
                            var segmentStart = _textPosition - _pending.Length;
                            throw new FormatException(
                                $"Encoded stream has an incomplete identity symbol at text position {segmentStart}. " +
                                $"Pending input is '{AlphabetValidator.EscapeForMessage(pending)}'.");
                        }

                        _pending.Clear();
                        if (AddToken(exact, out decodedByte))
                        {
                            EnsureCompleteByteGroup();
                            return true;
                        }
                    }
                }
                else if (_pending.Length > 0)
                {
                    var segmentStart = _textPosition - _pending.Length;
                    throw new FormatException(
                        $"Encoded stream has an incomplete identity symbol at text position {segmentStart}. " +
                        $"Pending input is '{AlphabetValidator.EscapeForMessage(_pending.ToString())}'.");
                }

                EnsureCompleteByteGroup();
                decodedByte = 0;
                return false;
            }

            private bool AddToken(TextToken token, out byte decodedByte)
            {
                if (_codec.Mode != EncodingMode.Packed)
                {
                    return AddDigit(token.Index, out decodedByte);
                }

                if (token.IsPadding)
                {
                    _packedSawPadding = true;
                    _packedPaddingCount++;
                    _symbolIndex++;
                    decodedByte = 0;
                    return false;
                }

                if (_packedSawPadding)
                {
                    throw new FormatException(
                        $"Packed input contains a data symbol after padding at symbol index {_symbolIndex}.");
                }

                var bitsPerSymbol = _codec.PackedBitsPerSymbol;
                _packedBuffer = (_packedBuffer << bitsPerSymbol) | token.Index;
                _packedBits += bitsPerSymbol;
                _packedDataSymbolCount++;
                _symbolIndex++;
                if (_packedBits < 8)
                {
                    decodedByte = 0;
                    return false;
                }

                _packedBits -= 8;
                decodedByte = (byte)((_packedBuffer >> _packedBits) & byte.MaxValue);
                _packedBuffer = _packedBits == 0
                    ? 0
                    : _packedBuffer & ((1 << _packedBits) - 1);
                return true;
            }

            private bool AddDigit(int digit, out byte decodedByte)
            {
                if (_groupValue > (byte.MaxValue - digit) / _codec.Identity.Count)
                {
                    throw new FormatException(
                        $"Encoded byte group {_byteGroup} exceeds 255 at symbol index {_symbolIndex}: " +
                        $"accumulated value {_groupValue}, digit {digit}, base {_codec.Identity.Count}.");
                }

                _groupValue = _groupValue * _codec.Identity.Count + digit;
                _groupOffset++;
                _symbolIndex++;
                if (_groupOffset < _codec.Size)
                {
                    decodedByte = 0;
                    return false;
                }

                decodedByte = (byte)_groupValue;
                _byteGroup++;
                _groupOffset = 0;
                _groupValue = 0;
                return true;
            }

            private void EnsureCompleteByteGroup()
            {
                if (_codec.Mode == EncodingMode.Packed)
                {
                    _codec.ValidatePackedTerminal(
                        _packedDataSymbolCount,
                        _packedPaddingCount,
                        _packedBits,
                        _packedBuffer);
                    return;
                }

                if (_groupOffset == 0)
                {
                    return;
                }

                var incompleteGroupStart = _symbolIndex - _groupOffset;
                throw new FormatException(
                    $"Encoded symbol count {_symbolIndex} must be a multiple of Size {_codec.Size}. " +
                    $"Incomplete byte group {_byteGroup} starts at symbol index {incompleteGroupStart}.");
            }
        }

        private sealed class TextToken
        {
            public TextToken(string text, int index, string? separator, bool isPadding = false)
            {
                Text = text;
                Index = index;
                BoundaryText = separator == null ? text : text + separator;
                IsPadding = isPadding;
            }

            public string Text { get; }

            public int Index { get; }

            public string BoundaryText { get; }

            public bool IsPadding { get; }
        }
    }
}
