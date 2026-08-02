using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NumeralSystems.Net;

namespace AnyBase.Net
{
    /// <summary>
    /// Encodes bytes and UTF-8 text with an ordered alphabet.
    /// </summary>
    /// <typeparam name="TBase">The symbol type used by the alphabet.</typeparam>
    public class Base<TBase> : IBase<TBase>
        where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly IReadOnlyDictionary<TBase, int> _indices;
        private int _size;

        /// <summary>
        /// Gets the symbols in numeric order, where the first symbol represents zero.
        /// </summary>
        public IReadOnlyList<TBase> Identity { get; }

        /// <summary>
        /// Gets the comparer used to validate and look up symbols.
        /// </summary>
        public IEqualityComparer<TBase> Comparer { get; }

        /// <summary>
        /// Gets or sets the fixed number of symbols used to encode each byte.
        /// </summary>
        /// <remarks>
        /// The value can be increased to add leading zero symbols, but cannot be lower
        /// than the number of digits needed to represent a byte in the selected base.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value is smaller than the minimum required to represent a byte.
        /// </exception>
        public int Size
        {
            get => _size;
            set
            {
                if (value < NumeralSystem.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        $"Size must be at least {NumeralSystem.Length} for base {Identity.Count}.");
                }

                _size = value;
            }
        }

        /// <summary>
        /// Gets the underlying numeral system.
        /// </summary>
        public NumeralSystem NumeralSystem { get; }

        /// <summary>
        /// Initializes a base from a set of unique symbols.
        /// </summary>
        /// <remarks>
        /// This overload is retained for compatibility. Prefer the enumerable overload
        /// when the alphabet order must be explicit.
        /// </remarks>
        /// <param name="identity">The alphabet symbols.</param>
        /// <exception cref="ArgumentNullException"><paramref name="identity"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// The alphabet contains fewer than two symbols, duplicate symbols, or a null symbol.
        /// </exception>
        public Base(HashSet<TBase> identity)
            : this(
                (IEnumerable<TBase>)identity,
                identity?.Comparer ?? EqualityComparer<TBase>.Default)
        {
        }

        /// <summary>
        /// Initializes a base from symbols supplied in numeric order.
        /// </summary>
        /// <param name="identity">The ordered alphabet. At least two unique symbols are required.</param>
        /// <exception cref="ArgumentNullException"><paramref name="identity"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// The alphabet contains fewer than two symbols, duplicate symbols, or a null symbol.
        /// </exception>
        public Base(IEnumerable<TBase> identity)
            : this(identity, EqualityComparer<TBase>.Default)
        {
        }

        /// <summary>
        /// Initializes a base from ordered symbols using a custom comparer.
        /// </summary>
        /// <param name="identity">The ordered alphabet. At least two unique symbols are required.</param>
        /// <param name="comparer">The comparer used for symbol uniqueness and lookup.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="identity"/> or <paramref name="comparer"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The alphabet contains fewer than two symbols, duplicate symbols, or a null symbol.
        /// </exception>
        public Base(IEnumerable<TBase> identity, IEqualityComparer<TBase> comparer)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            var values = identity.ToList();
            var validation = AlphabetValidator.ValidateMaterialized(
                values,
                comparer,
                separator: null,
                useSeparator: false);
            var structuralError = validation.Diagnostics.FirstOrDefault(diagnostic => !diagnostic.TextOperationsOnly);
            if (structuralError != null)
            {
                throw new ArgumentException(structuralError.Message, nameof(identity));
            }

            Identity = Array.AsReadOnly(values.ToArray());
            Comparer = comparer;
            _indices = values
                .Select((symbol, index) => new KeyValuePair<TBase, int>(symbol, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value, comparer);
            NumeralSystem = Numeral.System.OfBase(Identity.Count);
            _size = NumeralSystem.Length;
        }

        /// <summary>
        /// Calculates the exact number of identity symbols needed to encode a byte count.
        /// </summary>
        /// <param name="byteCount">The non-negative number of source bytes.</param>
        /// <returns>The exact encoded symbol count.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="byteCount"/> is negative.</exception>
        /// <exception cref="OverflowException">The encoded symbol count exceeds <see cref="int.MaxValue"/>.</exception>
        public int GetEncodedLength(int byteCount)
        {
            if (byteCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "Byte count cannot be negative.");
            }

            return checked(byteCount * Size);
        }

        /// <summary>
        /// Calculates the exact byte count represented by a complete encoded symbol sequence.
        /// </summary>
        /// <param name="symbolCount">The non-negative encoded symbol count.</param>
        /// <returns>The exact decoded byte count.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="symbolCount"/> is negative.</exception>
        /// <exception cref="FormatException"><paramref name="symbolCount"/> is not a multiple of <see cref="Size"/>.</exception>
        public int GetDecodedLength(int symbolCount)
        {
            if (symbolCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(symbolCount),
                    symbolCount,
                    "Symbol count cannot be negative.");
            }

            if (!TryGetDecodedLength(symbolCount, out var byteCount))
            {
                var incompleteGroupIndex = symbolCount / Size;
                var incompleteGroupStart = incompleteGroupIndex * Size;
                throw new FormatException(
                    $"Encoded symbol count {symbolCount} must be a multiple of Size {Size}. " +
                    $"Incomplete byte group {incompleteGroupIndex} starts at symbol index {incompleteGroupStart}.");
            }

            return byteCount;
        }

        /// <summary>
        /// Tries to calculate the byte count represented by an encoded symbol count.
        /// </summary>
        /// <param name="symbolCount">The encoded symbol count.</param>
        /// <param name="byteCount">The decoded byte count, or zero when the count is invalid.</param>
        /// <returns><see langword="true"/> when <paramref name="symbolCount"/> is non-negative and complete.</returns>
        public bool TryGetDecodedLength(int symbolCount, out int byteCount)
        {
            if (symbolCount < 0 || symbolCount % Size != 0)
            {
                byteCount = 0;
                return false;
            }

            byteCount = symbolCount / Size;
            return true;
        }

        /// <summary>
        /// Calculates the maximum number of UTF-16 characters needed to encode a byte count as text.
        /// </summary>
        /// <param name="byteCount">The non-negative source byte count.</param>
        /// <param name="separator">
        /// An optional non-empty separator between symbols; <see langword="null"/> means concatenated text.
        /// </param>
        /// <returns>The maximum encoded text length.</returns>
        public int GetMaxEncodedTextLength(int byteCount, string? separator = null)
        {
            var tokens = separator == null
                ? GetTextTokensForStringOperations()
                : GetTextTokensForSeparatedStringOperations(separator);
            var symbolCount = GetEncodedLength(byteCount);
            if (symbolCount == 0)
            {
                return 0;
            }

            var maximumTokenLength = tokens.Max(token => token.Text.Length);
            return checked(
                checked(symbolCount * maximumTokenLength) +
                checked((symbolCount - 1) * (separator?.Length ?? 0)));
        }

        /// <summary>
        /// Calculates the exact number of UTF-16 characters produced when encoding bytes as text.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <param name="separator">
        /// An optional non-empty separator between symbols; <see langword="null"/> means concatenated text.
        /// </param>
        /// <returns>The exact encoded text length.</returns>
        public int GetEncodedTextLength(ReadOnlySpan<byte> bytes, string? separator = null)
        {
            var tokens = separator == null
                ? GetTextTokensForStringOperations()
                : GetTextTokensForSeparatedStringOperations(separator);
            var textLength = 0;
            var symbolCount = GetEncodedLength(bytes.Length);

            for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
            {
                var value = (int)bytes[byteIndex];
                for (var offset = Size - 1; offset >= 0; offset--)
                {
                    var digit = value % Identity.Count;
                    textLength = checked(textLength + tokens[digit].Text.Length);
                    value /= Identity.Count;
                }
            }

            if (separator != null && symbolCount > 1)
            {
                textLength = checked(textLength + checked((symbolCount - 1) * separator.Length));
            }

            return textLength;
        }

        /// <summary>
        /// Encodes bytes into a caller-provided symbol span without allocating an output array.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <param name="destination">The destination symbol span.</param>
        /// <returns>The number of symbols written.</returns>
        /// <exception cref="ArgumentException"><paramref name="destination"/> is too small.</exception>
        public int Encode(ReadOnlySpan<byte> bytes, Span<TBase> destination)
        {
            var requiredLength = GetEncodedLength(bytes.Length);
            if (destination.Length < requiredLength)
            {
                throw new ArgumentException(
                    $"Destination length {destination.Length} is smaller than the required encoded length {requiredLength}.",
                    nameof(destination));
            }

            for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
            {
                var value = (int)bytes[byteIndex];
                var groupStart = byteIndex * Size;
                for (var offset = Size - 1; offset >= 0; offset--)
                {
                    destination[groupStart + offset] = Identity[value % Identity.Count];
                    value /= Identity.Count;
                }
            }

            return requiredLength;
        }

        /// <summary>
        /// Encodes byte memory into caller-provided symbol memory.
        /// </summary>
        /// <param name="bytes">The source byte memory.</param>
        /// <param name="destination">The destination symbol memory.</param>
        /// <returns>The number of symbols written.</returns>
        public int EncodeMemory(ReadOnlyMemory<byte> bytes, Memory<TBase> destination)
        {
            return Encode(bytes.Span, destination.Span);
        }

        /// <summary>
        /// Decodes identity symbols into a caller-provided byte span without allocating an output array.
        /// </summary>
        /// <param name="encoded">The encoded identity symbols.</param>
        /// <param name="destination">The destination byte span.</param>
        /// <returns>The number of bytes written.</returns>
        /// <exception cref="ArgumentException"><paramref name="destination"/> is too small.</exception>
        /// <exception cref="FormatException">The input is incomplete, contains an unknown symbol, or exceeds 255.</exception>
        public int Decode(ReadOnlySpan<TBase> encoded, Span<byte> destination)
        {
            var requiredLength = GetDecodedLength(encoded.Length);
            if (destination.Length < requiredLength)
            {
                throw new ArgumentException(
                    $"Destination length {destination.Length} is smaller than the required decoded length {requiredLength}.",
                    nameof(destination));
            }

            for (var group = 0; group < requiredLength; group++)
            {
                var value = 0;
                for (var offset = 0; offset < Size; offset++)
                {
                    var symbolPosition = group * Size + offset;
                    var symbol = encoded[symbolPosition];
                    if (symbol is null || !_indices.TryGetValue(symbol, out var digit))
                    {
                        throw new FormatException(
                            $"Unknown identity symbol {DescribeSymbol(symbol!)} at symbol index {symbolPosition} " +
                            $"(byte group {group}, offset {offset}).");
                    }

                    if (value > (byte.MaxValue - digit) / Identity.Count)
                    {
                        throw new FormatException(
                            $"Encoded byte group {group} exceeds 255 at symbol index {symbolPosition}: " +
                            $"accumulated value {value}, digit {digit}, base {Identity.Count}.");
                    }

                    value = value * Identity.Count + digit;
                }

                destination[group] = (byte)value;
            }

            return requiredLength;
        }

        /// <summary>
        /// Decodes identity symbol memory into caller-provided byte memory.
        /// </summary>
        /// <param name="encoded">The encoded symbol memory.</param>
        /// <param name="destination">The destination byte memory.</param>
        /// <returns>The number of bytes written.</returns>
        public int DecodeMemory(ReadOnlyMemory<TBase> encoded, Memory<byte> destination)
        {
            return Decode(encoded.Span, destination.Span);
        }

        /// <inheritdoc />
        public TBase[] Encode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var output = new TBase[GetEncodedLength(bytes.Length)];
            Encode(bytes.AsSpan(), output.AsSpan());
            return output;
        }

        /// <inheritdoc />
        public TBase[] Encode(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Encode(StrictUtf8.GetBytes(value));
        }

        /// <inheritdoc />
        public string EncodeToString(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var tokens = GetTextTokensForStringOperations();
            return string.Concat(Encode(bytes).Select(symbol => tokens[_indices[symbol]].Text));
        }

        /// <inheritdoc />
        public string EncodeToString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var tokens = GetTextTokensForStringOperations();
            return string.Concat(Encode(value).Select(symbol => tokens[_indices[symbol]].Text));
        }

        /// <summary>
        /// Encodes bytes and joins their identity symbols with an exact separator.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <param name="separator">The non-empty separator placed between symbols.</param>
        /// <returns>The separated encoded symbols.</returns>
        public string EncodeToString(byte[] bytes, string separator)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var tokens = GetTextTokensForSeparatedStringOperations(separator);
            return string.Join(separator, Encode(bytes).Select(symbol => tokens[_indices[symbol]].Text));
        }

        /// <summary>
        /// Encodes UTF-8 text and joins its identity symbols with an exact separator.
        /// </summary>
        /// <param name="value">The text to encode.</param>
        /// <param name="separator">The non-empty separator placed between symbols.</param>
        /// <returns>The separated encoded symbols.</returns>
        public string EncodeToString(string value, string separator)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var tokens = GetTextTokensForSeparatedStringOperations(separator);
            return string.Join(separator, Encode(value).Select(symbol => tokens[_indices[symbol]].Text));
        }

        /// <inheritdoc />
        public string DecodeToString(string encoded)
        {
            return DecodeUtf8(DecodeToBytes(encoded));
        }

        /// <summary>
        /// Parses separated identity symbols and decodes the resulting UTF-8 bytes.
        /// </summary>
        /// <param name="encoded">The separated encoded symbols.</param>
        /// <param name="separator">The exact non-empty separator between symbols.</param>
        /// <returns>The decoded text.</returns>
        public string DecodeToString(string encoded, string separator)
        {
            return DecodeUtf8(DecodeToBytes(encoded, separator));
        }

        /// <inheritdoc />
        public string DecodeToString(TBase[] encoded)
        {
            return DecodeUtf8(DecodeToBytes(encoded));
        }

        /// <summary>
        /// Parses concatenated identity symbols and decodes them into bytes.
        /// </summary>
        /// <param name="encoded">The concatenated encoded symbols.</param>
        /// <returns>The decoded bytes.</returns>
        public byte[] DecodeToBytes(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            var tokens = GetTextTokensForStringOperations();
            if (encoded.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var orderedTokens = tokens
                .OrderByDescending(token => token.Text.Length)
                .ThenBy(token => token.Index)
                .ToArray();
            var symbols = new List<TBase>();

            for (var position = 0; position < encoded.Length;)
            {
                var token = orderedTokens.FirstOrDefault(candidate =>
                    candidate.Text.Length <= encoded.Length - position &&
                    string.CompareOrdinal(encoded, position, candidate.Text, 0, candidate.Text.Length) == 0);

                if (token == null)
                {
                    var previewLength = Math.Min(16, encoded.Length - position);
                    var preview = AlphabetValidator.EscapeForMessage(encoded.Substring(position, previewLength));
                    throw new FormatException(
                        $"Encoded text has no identity symbol matching at text position {position}. " +
                        $"Remaining input starts with '{preview}'.");
                }

                symbols.Add(Identity[token.Index]);
                position += token.Text.Length;
            }

            return DecodeToBytes(symbols.ToArray());
        }

        /// <summary>
        /// Parses separated identity symbols and decodes them into bytes.
        /// </summary>
        /// <param name="encoded">The separated encoded symbols.</param>
        /// <param name="separator">The exact non-empty separator between symbols.</param>
        /// <returns>The decoded bytes.</returns>
        public byte[] DecodeToBytes(string encoded, string separator)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            var tokens = GetTextTokensForSeparatedStringOperations(separator);
            if (encoded.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var symbols = new List<TBase>();
            for (var textPosition = 0; textPosition < encoded.Length;)
            {
                var matchingTokens = tokens
                    .Where(token =>
                        TextMatchesAt(encoded, token.Text, textPosition) &&
                        (textPosition + token.Text.Length == encoded.Length ||
                         TextMatchesAt(encoded, separator, textPosition + token.Text.Length)))
                    .ToArray();

                if (matchingTokens.Length == 0)
                {
                    var previewLength = Math.Min(16, encoded.Length - textPosition);
                    var preview = AlphabetValidator.EscapeForMessage(
                        encoded.Substring(textPosition, previewLength));
                    throw new FormatException(
                        $"Encoded text has no complete identity symbol followed by separator " +
                        $"'{AlphabetValidator.EscapeForMessage(separator)}' at symbol index {symbols.Count} " +
                        $"(text position {textPosition}). Remaining input starts with '{preview}'.");
                }

                if (matchingTokens.Length > 1)
                {
                    throw new FormatException(
                        $"Encoded text is ambiguous at symbol index {symbols.Count} (text position {textPosition}); " +
                        $"multiple identity symbols are followed by separator " +
                        $"'{AlphabetValidator.EscapeForMessage(separator)}'.");
                }

                var token = matchingTokens[0];
                symbols.Add(Identity[token.Index]);
                textPosition += token.Text.Length;
                if (textPosition == encoded.Length)
                {
                    break;
                }

                textPosition += separator.Length;
                if (textPosition == encoded.Length)
                {
                    throw new FormatException(
                        $"Encoded text ends with separator '{AlphabetValidator.EscapeForMessage(separator)}' " +
                        $"at text position {textPosition - separator.Length}.");
                }
            }

            return DecodeToBytes(symbols.ToArray());
        }

        /// <inheritdoc />
        public byte[] DecodeToBytes(TBase[] encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (encoded.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var output = new byte[GetDecodedLength(encoded.Length)];
            Decode(encoded.AsSpan(), output.AsSpan());
            return output;
        }

        internal int GetIdentityIndex(TBase symbol)
        {
            if (symbol != null && _indices.TryGetValue(symbol, out var index))
            {
                return index;
            }

            throw new InvalidOperationException("The encoded symbol is not present in the codec identity.");
        }

        private string DecodeUtf8(byte[] bytes)
        {
            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new DecoderFallbackException(
                    $"Decoded bytes are not valid UTF-8 at byte index {exception.Index}.",
                    exception.BytesUnknown ?? Array.Empty<byte>(),
                    exception.Index);
            }
        }

        private SymbolToken[] GetTextTokensForStringOperations()
        {
            var validation = AlphabetValidator.ValidateMaterialized(
                Identity,
                Comparer,
                separator: null,
                useSeparator: false);
            ThrowIfTextIncompatible(validation, nameof(Identity));
            return CreateTextTokens();
        }

        private SymbolToken[] GetTextTokensForSeparatedStringOperations(string separator)
        {
            if (separator == null)
            {
                throw new ArgumentNullException(nameof(separator));
            }

            var validation = AlphabetValidator.ValidateMaterialized(
                Identity,
                Comparer,
                separator,
                useSeparator: true);
            ThrowIfTextIncompatible(validation, nameof(separator));
            return CreateTextTokens();
        }

        private SymbolToken[] CreateTextTokens()
        {
            return Identity
                .Select((symbol, index) => new SymbolToken(AlphabetValidator.SymbolText(symbol), index))
                .ToArray();
        }

        private static void ThrowIfTextIncompatible(AlphabetValidationResult validation, string parameterName)
        {
            var diagnostic = validation.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                return;
            }

            if (diagnostic.Kind == AlphabetValidationDiagnosticKind.EmptySeparator ||
                diagnostic.Kind == AlphabetValidationDiagnosticKind.SeparatorCollision)
            {
                throw new ArgumentException(diagnostic.Message, parameterName);
            }

            throw new InvalidOperationException(diagnostic.Message);
        }

        private static string DescribeSymbol(TBase symbol)
        {
            return symbol is null
                ? "<null>"
                : $"'{AlphabetValidator.EscapeForMessage(AlphabetValidator.SymbolText(symbol))}'";
        }

        private static bool TextMatchesAt(string value, string candidate, int position)
        {
            return position >= 0 &&
                   candidate.Length <= value.Length - position &&
                   string.CompareOrdinal(value, position, candidate, 0, candidate.Length) == 0;
        }

        private sealed class SymbolToken
        {
            public SymbolToken(string text, int index)
            {
                Text = text;
                Index = index;
            }

            public string Text { get; }

            public int Index { get; }
        }
    }
}
