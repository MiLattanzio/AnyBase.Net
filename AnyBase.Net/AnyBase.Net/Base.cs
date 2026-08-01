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
        public Base(HashSet<TBase> identity)
            : this((IEnumerable<TBase>)identity)
        {
        }

        /// <summary>
        /// Initializes a base from symbols supplied in numeric order.
        /// </summary>
        /// <param name="identity">The ordered alphabet. At least two unique symbols are required.</param>
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
            return string.Concat(Encode(bytes).Select(SymbolText));
        }

        /// <inheritdoc />
        public string EncodeToString(string value)
        {
            return string.Concat(Encode(value).Select(SymbolText));
        }

        /// <inheritdoc />
        public string DecodeToString(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (encoded.Length == 0)
            {
                return string.Empty;
            }

            var tokens = Identity
                .Select((symbol, index) => new SymbolToken(SymbolText(symbol), index))
                .ToArray();

            if (tokens.Any(token => token.Text.Length == 0))
            {
                throw new InvalidOperationException(
                    "String decoding is unavailable when an identity symbol has an empty text representation.");
            }

            if (tokens.Select(token => token.Text).Distinct(StringComparer.Ordinal).Count() != tokens.Length)
            {
                throw new InvalidOperationException(
                    "String decoding requires every identity symbol to have a unique text representation.");
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
                    throw new FormatException($"Unknown identity symbol at position {position}.");
                }

                symbols.Add(Identity[token.Index]);
                position += token.Text.Length;
            }

            return DecodeToString(symbols.ToArray());
        }

        /// <inheritdoc />
        public string DecodeToString(TBase[] encoded)
        {
            return StrictUtf8.GetString(DecodeToBytes(encoded));
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
                throw new FormatException($"Encoded input length must be a multiple of {Size}.");
            }

            var output = new byte[encoded.Length / Size];
            for (var group = 0; group < output.Length; group++)
            {
                var value = 0;
                for (var offset = 0; offset < Size; offset++)
                {
                    var symbolPosition = group * Size + offset;
                    if (!_indices.TryGetValue(encoded[symbolPosition], out var digit))
                    {
                        throw new FormatException($"Unknown identity symbol at index {symbolPosition}.");
                    }

                    if (value > (byte.MaxValue - digit) / Identity.Count)
                    {
                        throw new FormatException($"Encoded group at index {group} exceeds the byte range.");
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
            return symbol.ToString() ?? string.Empty;
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
