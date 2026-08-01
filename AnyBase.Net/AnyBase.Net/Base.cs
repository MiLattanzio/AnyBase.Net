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
            : this((IEnumerable<TBase>)identity)
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
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            var values = identity.ToList();
            if (values.Count < 2)
            {
                throw new ArgumentException("Identity must contain at least two symbols.", nameof(identity));
            }

            if (values.Any(value => value is null))
            {
                throw new ArgumentException("Identity cannot contain null symbols.", nameof(identity));
            }

            if (values.Distinct().Count() != values.Count)
            {
                throw new ArgumentException("Identity symbols must be unique.", nameof(identity));
            }

            Identity = Array.AsReadOnly(values.ToArray());
            _indices = values
                .Select((symbol, index) => new KeyValuePair<TBase, int>(symbol, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            NumeralSystem = Numeral.System.OfBase(Identity.Count);
            _size = NumeralSystem.Length;
        }

        /// <inheritdoc />
        public TBase[] Encode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return bytes
                .Select(NumeralForByte)
                .SelectMany(numeral => numeral.IntegralIndices.Select(index => Identity[index]))
                .ToArray();
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

        /// <inheritdoc />
        public string DecodeToString(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            var tokens = GetTextTokensForStringOperations();

            if (encoded.Length == 0)
            {
                return string.Empty;
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
                    var preview = EscapeForMessage(encoded.Substring(position, previewLength));
                    throw new FormatException(
                        $"Encoded text has no identity symbol matching at text position {position}. " +
                        $"Remaining input starts with '{preview}'.");
                }

                symbols.Add(Identity[token.Index]);
                position += token.Text.Length;
            }

            return DecodeToString(symbols.ToArray());
        }

        /// <inheritdoc />
        public string DecodeToString(TBase[] encoded)
        {
            var bytes = DecodeToBytes(encoded);
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

            if (encoded.Length % Size != 0)
            {
                var incompleteGroupIndex = encoded.Length / Size;
                var incompleteGroupStart = incompleteGroupIndex * Size;
                throw new FormatException(
                    $"Encoded symbol count {encoded.Length} must be a multiple of Size {Size}. " +
                    $"Incomplete byte group {incompleteGroupIndex} starts at symbol index {incompleteGroupStart}.");
            }

            var output = new byte[encoded.Length / Size];
            for (var group = 0; group < output.Length; group++)
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

                output[group] = (byte)value;
            }

            return output;
        }

        private Numeral NumeralForByte(byte value)
        {
            var numeral = NumeralSystem[value];
            if (numeral.IntegralIndices.Count == Size)
            {
                return numeral;
            }

            if (numeral.IntegralIndices.Count > Size)
            {
                throw new InvalidOperationException("The configured size cannot represent a byte.");
            }

            var integral = Enumerable
                .Repeat(0, Size - numeral.IntegralIndices.Count)
                .Concat(numeral.IntegralIndices)
                .ToList();
            return new Numeral(numeral.Base, integral, numeral.FractionalIndices, numeral.Positive);
        }

        private static string SymbolText(TBase symbol)
        {
            return symbol is null ? string.Empty : symbol.ToString() ?? string.Empty;
        }

        private SymbolToken[] GetTextTokensForStringOperations()
        {
            var tokens = Identity
                .Select((symbol, index) => new SymbolToken(SymbolText(symbol), index))
                .ToArray();

            var empty = tokens.FirstOrDefault(token => token.Text.Length == 0);
            if (empty != null)
            {
                throw new InvalidOperationException(
                    $"Identity symbol at index {empty.Index} has an empty text representation. " +
                    "String encoding and decoding require non-empty symbol text; use the symbol-array APIs instead.");
            }

            for (var firstIndex = 0; firstIndex < tokens.Length; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < tokens.Length; secondIndex++)
                {
                    var first = tokens[firstIndex];
                    var second = tokens[secondIndex];
                    if (string.Equals(first.Text, second.Text, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Identity symbols at indices {first.Index} and {second.Index} both use the text " +
                            $"'{EscapeForMessage(first.Text)}'. String encoding and decoding require unique symbol text; " +
                            "use the symbol-array APIs instead.");
                    }

                    var shorter = first.Text.Length < second.Text.Length ? first : second;
                    var longer = ReferenceEquals(shorter, first) ? second : first;
                    if (longer.Text.StartsWith(shorter.Text, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Identity symbol text '{EscapeForMessage(shorter.Text)}' at index {shorter.Index} is a prefix " +
                            $"of '{EscapeForMessage(longer.Text)}' at index {longer.Index}. String encoding and decoding " +
                            "require a prefix-free textual alphabet; use the symbol-array APIs instead.");
                    }
                }
            }

            return tokens;
        }

        private static string DescribeSymbol(TBase symbol)
        {
            return symbol is null ? "<null>" : $"'{EscapeForMessage(SymbolText(symbol))}'";
        }

        private static string EscapeForMessage(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0");
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
