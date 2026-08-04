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
        private readonly bool _supportsPadding;
        private readonly TBase _paddingSymbol = default!;
        private EncodingMode _mode;
        private bool _usePadding;
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
        /// Gets or sets how bytes are mapped to alphabet symbols.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="EncodingMode.FixedWidthByte"/> for compatibility
        /// with every AnyBase.Net 1.x release. Packed mode requires an alphabet whose
        /// size is a power of two between 2 and 256.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined encoding mode.</exception>
        /// <exception cref="InvalidOperationException">
        /// Packed mode is selected for an alphabet whose size is not a power of two from 2 through 256.
        /// </exception>
        public EncodingMode Mode
        {
            get => _mode;
            set
            {
                ValidateEncodingMode(value);
                if (value == EncodingMode.Packed)
                {
                    ValidatePackedAlphabet();
                }

                _mode = value;
            }
        }

        /// <summary>
        /// Gets whether this codec was configured with a padding symbol.
        /// </summary>
        public bool SupportsPadding => _supportsPadding;

        /// <summary>
        /// Gets the padding symbol configured for packed output.
        /// </summary>
        /// <exception cref="InvalidOperationException">No padding symbol was configured.</exception>
        public TBase PaddingSymbol => _supportsPadding
            ? _paddingSymbol
            : throw new InvalidOperationException("This codec has no padding symbol configured.");

        /// <summary>
        /// Gets or sets whether packed output is completed to a full encoding quantum.
        /// </summary>
        /// <remarks>
        /// RFC 4648 Base32 and Base64 factories enable padding by default. Disabling it
        /// produces the common unpadded form and makes the decoder require that form.
        /// This property has no effect in fixed-width mode.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The value is set to <see langword="true"/> but no padding symbol is configured.
        /// </exception>
        public bool UsePadding
        {
            get => _usePadding;
            set
            {
                if (value && !_supportsPadding)
                {
                    throw new InvalidOperationException("A padding symbol must be configured before enabling padding.");
                }

                _usePadding = value;
            }
        }

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
            : this(
                identity,
                comparer,
                EncodingMode.FixedWidthByte,
                hasPaddingSymbol: false,
                paddingSymbol: default!,
                usePadding: false)
        {
        }

        /// <summary>
        /// Initializes a base using an explicit encoding mode.
        /// </summary>
        /// <param name="identity">The ordered alphabet. At least two unique symbols are required.</param>
        /// <param name="mode">The byte-to-symbol encoding mode.</param>
        /// <exception cref="ArgumentNullException"><paramref name="identity"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not defined.</exception>
        /// <exception cref="InvalidOperationException">
        /// Packed mode is selected for an alphabet whose size is not a power of two from 2 through 256.
        /// </exception>
        public Base(IEnumerable<TBase> identity, EncodingMode mode)
            : this(identity, EqualityComparer<TBase>.Default, mode)
        {
        }

        /// <summary>
        /// Initializes a base using a custom comparer and explicit encoding mode.
        /// </summary>
        /// <param name="identity">The ordered alphabet. At least two unique symbols are required.</param>
        /// <param name="comparer">The comparer used for symbol uniqueness and lookup.</param>
        /// <param name="mode">The byte-to-symbol encoding mode.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="identity"/> or <paramref name="comparer"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not defined.</exception>
        /// <exception cref="InvalidOperationException">
        /// Packed mode is selected for an alphabet whose size is not a power of two from 2 through 256.
        /// </exception>
        public Base(
            IEnumerable<TBase> identity,
            IEqualityComparer<TBase> comparer,
            EncodingMode mode)
            : this(
                identity,
                comparer,
                mode,
                hasPaddingSymbol: false,
                paddingSymbol: default!,
                usePadding: false)
        {
        }

        /// <summary>
        /// Initializes a base using an explicit encoding mode and padding configuration.
        /// </summary>
        /// <param name="identity">The ordered alphabet.</param>
        /// <param name="comparer">The comparer used for symbol uniqueness and lookup.</param>
        /// <param name="mode">The encoding mode.</param>
        /// <param name="paddingSymbol">A symbol that does not occur in the alphabet.</param>
        /// <param name="usePadding">Whether packed encodings include and require padding.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="identity"/>, <paramref name="comparer"/>, or <paramref name="paddingSymbol"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The alphabet is invalid or contains <paramref name="paddingSymbol"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not defined.</exception>
        /// <exception cref="InvalidOperationException">
        /// Packed mode is selected for an alphabet whose size is not a power of two from 2 through 256.
        /// </exception>
        public Base(
            IEnumerable<TBase> identity,
            IEqualityComparer<TBase> comparer,
            EncodingMode mode,
            TBase paddingSymbol,
            bool usePadding)
            : this(identity, comparer, mode, true, paddingSymbol, usePadding)
        {
        }

        private Base(
            IEnumerable<TBase> identity,
            IEqualityComparer<TBase> comparer,
            EncodingMode mode,
            bool hasPaddingSymbol,
            TBase paddingSymbol,
            bool usePadding)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            ValidateEncodingMode(mode);

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
            _supportsPadding = hasPaddingSymbol;
            if (hasPaddingSymbol)
            {
                if (paddingSymbol is null)
                {
                    throw new ArgumentNullException(nameof(paddingSymbol));
                }

                if (_indices.ContainsKey(paddingSymbol))
                {
                    throw new ArgumentException("The padding symbol must not occur in the alphabet.", nameof(paddingSymbol));
                }

                _paddingSymbol = paddingSymbol;
            }

            _mode = mode;
            if (mode == EncodingMode.Packed)
            {
                ValidatePackedAlphabet();
            }

            UsePadding = usePadding;
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

            return Mode == EncodingMode.FixedWidthByte
                ? checked(byteCount * Size)
                : GetPackedEncodedLength(byteCount);
        }

        /// <summary>
        /// Calculates the byte buffer size needed for an encoded symbol count.
        /// </summary>
        /// <param name="symbolCount">The non-negative encoded symbol count.</param>
        /// <returns>
        /// The exact decoded byte count in fixed-width mode; in packed mode, the maximum
        /// byte count because a count alone does not identify trailing padding symbols.
        /// Use <see cref="GetDecodedLength(ReadOnlySpan{TBase})"/> for the exact packed length.
        /// </returns>
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

            if (Mode == EncodingMode.Packed)
            {
                return checked((int)(checked((long)symbolCount * GetPackedBitsPerSymbol()) / 8));
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
        /// Calculates the exact decoded byte count after validating packed padding and symbol layout.
        /// </summary>
        /// <param name="encoded">The encoded symbols.</param>
        /// <returns>The exact decoded byte count.</returns>
        /// <exception cref="FormatException">The packed symbol count, padding, or pad bits are invalid.</exception>
        public int GetDecodedLength(ReadOnlySpan<TBase> encoded)
        {
            return Mode == EncodingMode.FixedWidthByte
                ? GetDecodedLength(encoded.Length)
                : AnalyzePacked(encoded).DecodedLength;
        }

        /// <summary>
        /// Tries to calculate the byte buffer size needed for an encoded symbol count.
        /// </summary>
        /// <param name="symbolCount">The encoded symbol count.</param>
        /// <param name="byteCount">
        /// The exact fixed-width byte count or the maximum packed byte count; zero when invalid.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="symbolCount"/> is non-negative and,
        /// in fixed-width mode, contains complete byte groups.
        /// </returns>
        public bool TryGetDecodedLength(int symbolCount, out int byteCount)
        {
            if (symbolCount < 0 ||
                (Mode == EncodingMode.FixedWidthByte && symbolCount % Size != 0))
            {
                byteCount = 0;
                return false;
            }

            byteCount = Mode == EncodingMode.FixedWidthByte
                ? symbolCount / Size
                : checked((int)(checked((long)symbolCount * GetPackedBitsPerSymbol()) / 8));
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
            if (Mode == EncodingMode.Packed)
            {
                var dataSymbolCount = GetPackedDataSymbolCount(byteCount);
                var paddingCount = symbolCount - dataSymbolCount;
                return checked(
                    checked(dataSymbolCount * maximumTokenLength) +
                    checked(paddingCount * GetPaddingText().Length));
            }

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

            if (Mode == EncodingMode.Packed)
            {
                return GetPackedEncodedTextLength(bytes, tokens);
            }

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

            if (Mode == EncodingMode.Packed)
            {
                return EncodePacked(bytes, destination);
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
            var requiredLength = GetDecodedLength(encoded);
            if (destination.Length < requiredLength)
            {
                throw new ArgumentException(
                    $"Destination length {destination.Length} is smaller than the required decoded length {requiredLength}.",
                    nameof(destination));
            }

            if (Mode == EncodingMode.Packed)
            {
                return DecodePacked(encoded, destination, requiredLength);
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
            return string.Concat(Encode(bytes).Select(symbol => GetEncodedSymbolText(symbol, tokens)));
        }

        /// <inheritdoc />
        public string EncodeToString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var tokens = GetTextTokensForStringOperations();
            return string.Concat(Encode(value).Select(symbol => GetEncodedSymbolText(symbol, tokens)));
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
            return string.Join(separator, Encode(bytes).Select(symbol => GetEncodedSymbolText(symbol, tokens)));
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
            return string.Join(separator, Encode(value).Select(symbol => GetEncodedSymbolText(symbol, tokens)));
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

            var orderedTokens = GetDecodingTextTokens(tokens)
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

                symbols.Add(token.Index >= 0 ? Identity[token.Index] : PaddingSymbol);
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

            var output = new byte[GetDecodedLength(encoded.AsSpan())];
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

        internal int PackedBitsPerSymbol => GetPackedBitsPerSymbol();

        internal int PackedInputQuantumBytes => GetPackedBitsPerSymbol() / GreatestCommonDivisor(8, GetPackedBitsPerSymbol());

        internal bool IsPaddingSymbol(TBase symbol)
        {
            return _supportsPadding && symbol != null && Comparer.Equals(symbol, _paddingSymbol);
        }

        internal void ValidatePackedTerminal(
            int dataSymbolCount,
            int paddingCount,
            int remainingBitCount,
            int remainingValue)
        {
            var bitsPerSymbol = GetPackedBitsPerSymbol();
            var decodedByteCount = checked((int)(checked((long)dataSymbolCount * bitsPerSymbol) / 8));
            var canonicalDataSymbolCount = decodedByteCount == 0
                ? 0
                : checked((int)((checked((long)decodedByteCount * 8) + bitsPerSymbol - 1) / bitsPerSymbol));
            if (canonicalDataSymbolCount != dataSymbolCount)
            {
                throw new FormatException(
                    $"Packed symbol count {dataSymbolCount} cannot represent a complete byte sequence " +
                    $"with {bitsPerSymbol} bits per symbol.");
            }

            if (remainingBitCount > 0 && remainingValue != 0)
            {
                throw new FormatException(
                    $"Packed input has non-zero pad bits in the final symbol; " +
                    $"the remaining {remainingBitCount} bits must be zero for a canonical encoding.");
            }

            var quantumSymbols = 8 / GreatestCommonDivisor(8, bitsPerSymbol);
            var remainder = dataSymbolCount % quantumSymbols;
            var requiredPadding = remainder == 0 ? 0 : quantumSymbols - remainder;
            if (UsePadding && paddingCount != requiredPadding)
            {
                throw new FormatException(
                    $"Packed input requires {requiredPadding} padding symbol(s) after {dataSymbolCount} data symbol(s), " +
                    $"but found {paddingCount}.");
            }

            if (!UsePadding && paddingCount != 0)
            {
                throw new FormatException("Packed input contains padding while padding is disabled.");
            }
        }

        private int GetPackedEncodedLength(int byteCount)
        {
            var dataSymbolCount = GetPackedDataSymbolCount(byteCount);
            if (!UsePadding || dataSymbolCount == 0)
            {
                return dataSymbolCount;
            }

            var bitsPerSymbol = GetPackedBitsPerSymbol();
            var quantumSymbols = 8 / GreatestCommonDivisor(8, bitsPerSymbol);
            return checked(((dataSymbolCount + quantumSymbols - 1) / quantumSymbols) * quantumSymbols);
        }

        private int GetPackedDataSymbolCount(int byteCount)
        {
            var bitsPerSymbol = GetPackedBitsPerSymbol();
            return byteCount == 0
                ? 0
                : checked((int)((checked((long)byteCount * 8) + bitsPerSymbol - 1) / bitsPerSymbol));
        }

        private int GetPackedEncodedTextLength(ReadOnlySpan<byte> bytes, SymbolToken[] tokens)
        {
            var bitsPerSymbol = GetPackedBitsPerSymbol();
            var buffer = 0;
            var bufferedBits = 0;
            var textLength = 0;
            var dataSymbolCount = 0;

            for (var index = 0; index < bytes.Length; index++)
            {
                buffer = (buffer << 8) | bytes[index];
                bufferedBits += 8;
                while (bufferedBits >= bitsPerSymbol)
                {
                    bufferedBits -= bitsPerSymbol;
                    var digit = (buffer >> bufferedBits) & (Identity.Count - 1);
                    textLength = checked(textLength + tokens[digit].Text.Length);
                    dataSymbolCount++;
                }

                buffer = bufferedBits == 0 ? 0 : buffer & ((1 << bufferedBits) - 1);
            }

            if (bufferedBits > 0)
            {
                var digit = (buffer << (bitsPerSymbol - bufferedBits)) & (Identity.Count - 1);
                textLength = checked(textLength + tokens[digit].Text.Length);
                dataSymbolCount++;
            }

            var paddingCount = GetEncodedLength(bytes.Length) - dataSymbolCount;
            return checked(textLength + checked(paddingCount * GetPaddingText().Length));
        }

        private int EncodePacked(ReadOnlySpan<byte> bytes, Span<TBase> destination)
        {
            var bitsPerSymbol = GetPackedBitsPerSymbol();
            var buffer = 0;
            var bufferedBits = 0;
            var written = 0;

            for (var index = 0; index < bytes.Length; index++)
            {
                buffer = (buffer << 8) | bytes[index];
                bufferedBits += 8;
                while (bufferedBits >= bitsPerSymbol)
                {
                    bufferedBits -= bitsPerSymbol;
                    destination[written++] = Identity[(buffer >> bufferedBits) & (Identity.Count - 1)];
                }

                buffer = bufferedBits == 0 ? 0 : buffer & ((1 << bufferedBits) - 1);
            }

            if (bufferedBits > 0)
            {
                destination[written++] = Identity[
                    (buffer << (bitsPerSymbol - bufferedBits)) & (Identity.Count - 1)];
            }

            var requiredLength = GetEncodedLength(bytes.Length);
            while (written < requiredLength)
            {
                destination[written++] = PaddingSymbol;
            }

            return written;
        }

        private int DecodePacked(
            ReadOnlySpan<TBase> encoded,
            Span<byte> destination,
            int requiredLength)
        {
            var analysis = AnalyzePacked(encoded);
            var bitsPerSymbol = GetPackedBitsPerSymbol();
            var buffer = 0;
            var bufferedBits = 0;
            var written = 0;

            for (var index = 0; index < analysis.DataSymbolCount; index++)
            {
                var digit = _indices[encoded[index]];
                buffer = (buffer << bitsPerSymbol) | digit;
                bufferedBits += bitsPerSymbol;
                if (bufferedBits >= 8)
                {
                    bufferedBits -= 8;
                    destination[written++] = (byte)((buffer >> bufferedBits) & byte.MaxValue);
                    buffer = bufferedBits == 0 ? 0 : buffer & ((1 << bufferedBits) - 1);
                }
            }

            if (written != requiredLength)
            {
                throw new InvalidOperationException("Packed decoding produced an unexpected byte count.");
            }

            return written;
        }

        private PackedAnalysis AnalyzePacked(ReadOnlySpan<TBase> encoded)
        {
            var bitsPerSymbol = GetPackedBitsPerSymbol();
            var buffer = 0;
            var bufferedBits = 0;
            var dataSymbolCount = 0;
            var paddingCount = 0;
            var sawPadding = false;

            for (var index = 0; index < encoded.Length; index++)
            {
                var symbol = encoded[index];
                if (IsPaddingSymbol(symbol))
                {
                    sawPadding = true;
                    paddingCount++;
                    continue;
                }

                if (sawPadding)
                {
                    throw new FormatException($"Packed input contains a data symbol after padding at symbol index {index}.");
                }

                if (symbol is null || !_indices.TryGetValue(symbol, out var digit))
                {
                    throw new FormatException(
                        $"Unknown identity symbol {DescribeSymbol(symbol!)} at packed symbol index {index}.");
                }

                buffer = (buffer << bitsPerSymbol) | digit;
                bufferedBits += bitsPerSymbol;
                if (bufferedBits >= 8)
                {
                    bufferedBits -= 8;
                    buffer = bufferedBits == 0 ? 0 : buffer & ((1 << bufferedBits) - 1);
                }

                dataSymbolCount++;
            }

            ValidatePackedTerminal(dataSymbolCount, paddingCount, bufferedBits, buffer);
            var decodedLength = checked((int)(checked((long)dataSymbolCount * bitsPerSymbol) / 8));
            return new PackedAnalysis(dataSymbolCount, paddingCount, decodedLength);
        }

        private string GetEncodedSymbolText(TBase symbol, SymbolToken[] tokens)
        {
            return IsPaddingSymbol(symbol)
                ? GetPaddingText()
                : tokens[GetIdentityIndex(symbol)].Text;
        }

        private string GetPaddingText()
        {
            return AlphabetValidator.SymbolText(PaddingSymbol);
        }

        private IEnumerable<SymbolToken> GetDecodingTextTokens(SymbolToken[] tokens)
        {
            if (Mode != EncodingMode.Packed || !_supportsPadding)
            {
                return tokens;
            }

            var paddingText = GetPaddingText();
            if (paddingText.Length == 0 || tokens.Any(token =>
                    token.Text.StartsWith(paddingText, StringComparison.Ordinal) ||
                    paddingText.StartsWith(token.Text, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "The padding symbol text must be non-empty and prefix-distinct from every alphabet symbol.");
            }

            return tokens.Concat(new[] { new SymbolToken(paddingText, -1) });
        }

        private static void ValidateEncodingMode(EncodingMode mode)
        {
            if (!Enum.IsDefined(typeof(EncodingMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown encoding mode.");
            }
        }

        private void ValidatePackedAlphabet()
        {
            var count = Identity.Count;
            if (count < 2 || count > 256 || (count & (count - 1)) != 0)
            {
                throw new InvalidOperationException(
                    $"Packed mode requires a power-of-two alphabet size between 2 and 256; found {count}.");
            }
        }

        private int GetPackedBitsPerSymbol()
        {
            ValidatePackedAlphabet();
            var bits = 0;
            for (var count = Identity.Count; count > 1; count >>= 1)
            {
                bits++;
            }

            return bits;
        }

        private static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                var remainder = left % right;
                left = right;
                right = remainder;
            }

            return left;
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
            var tokens = CreateTextTokens();
            _ = GetDecodingTextTokens(tokens).ToArray();
            return tokens;
        }

        private SymbolToken[] GetTextTokensForSeparatedStringOperations(string separator)
        {
            if (Mode == EncodingMode.Packed)
            {
                throw new InvalidOperationException(
                    "Packed mode does not support symbol separators because they are not part of RFC 4648 encodings.");
            }

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

        private readonly struct PackedAnalysis
        {
            public PackedAnalysis(int dataSymbolCount, int paddingCount, int decodedLength)
            {
                DataSymbolCount = dataSymbolCount;
                PaddingCount = paddingCount;
                DecodedLength = decodedLength;
            }

            public int DataSymbolCount { get; }

            public int PaddingCount { get; }

            public int DecodedLength { get; }
        }
    }
}
