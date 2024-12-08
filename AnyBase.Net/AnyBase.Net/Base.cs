using System;
using System.Collections.Generic;
using System.Linq;
using NumeralSystems.Net;
using NumeralSystems.Net.Utils;

namespace AnyBase.Net
{
    /// <summary>
    /// Represents a generic base encoding and decoding system for a specified type of base.
    /// </summary>
    /// <typeparam name="TBase">
    /// The type of the base elements, which must implement the <see cref="IComparable"/>, <see cref="IComparable{T}"/>,
    /// <see cref="IConvertible"/>, and <see cref="IEquatable{T}"/> interfaces.
    /// </typeparam>
    /// <remarks>
    /// The <c>Base</c> class provides functionality to encode and decode data into a numeral system defined by a specific set
    /// of values of type <c>TBase</c>. It supports operations to encode byte arrays and strings as well as decode encoded data back into strings or byte arrays.
    /// </remarks>
    public class Base<TBase>: IBase<TBase> where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
    {
        /// <summary>
        /// Gets the list of unique elements that define the base identity used for encoding and decoding operations.
        /// </summary>
        /// <remarks>
        /// The identity is a collection of elements that the base uses to represent data. Each element corresponds to a numeral
        /// in the numeral system. This property is essential for translation between numerical values and their encoded representations.
        /// </remarks>
        public IReadOnlyList<TBase> Identity { get; }

        /// <summary>
        /// Gets or sets the size utilized in encoding or decoding operations within the numeral system.
        /// </summary>
        /// <remarks>
        /// The Size property is critical for determining the length of encoded numerals in the numeral system processing.
        public int Size { get; set; }

        /// <summary>
        /// Represents the numeral system used for encoding and decoding operations within the Base class.
        /// Used to convert between different numeral systems and the identity of the Base class.
        /// </summary>
        public NumeralSystem NumeralSystem { get; }

        /// <summary>
        /// Represents a private instance of the NumeralSystem class for internal encoding and decoding operations.
        /// </summary>
        /// <remarks>
        /// This numeral system is initialized with the maximum value of a character and is used to convert
        /// between different numeral representations within the encoding and decoding methods of the Base class.
        /// </remarks>
        private readonly NumeralSystem _stringSystem = new NumeralSystem(char.MaxValue);

        /// <summary>
        /// Encodes a byte array into an array of the specified generic type TBase by converting each byte
        /// to its corresponding numeral using a predefined numeral system.
        /// </summary>
        /// <param name="bytes">The byte array to be encoded.</param>
        /// <returns>An array of type TBase representing the encoded bytes.</returns>
        public TBase[] Encode(byte[] bytes)
        {
            var indices = bytes.Select(b => (int)b).ToList();
            var numerals = indices.Select(x => _stringSystem[x]).ToArray();
            var encodedNumerals = numerals.Select(x => AdjustSize(x.To(NumeralSystem), Size)).ToArray();
            var output = encodedNumerals.Select(x => x.IntegralIndices.Select(y => Identity[y])).SelectMany(x => x).ToArray();
            return output;
        }

        /// <summary>
        /// Encodes the given string into an array of <typeparamref name="TBase"/> type.
        /// </summary>
        /// <param name="bytes">The string to be encoded.</param>
        /// <returns>An array of <typeparamref name="TBase"/> representing the encoded value of the input string.</returns>
        public TBase[] Encode(string bytes)
        {
            var chars = bytes.ToCharArray();
            var indices = chars.Select(b => (int)b).ToList();
            var numerals = indices.Select(x => _stringSystem[x]).ToArray();
            var encodedNumerals = numerals.Select(x => AdjustSize(x.To(NumeralSystem), Size)).ToArray();
            var output = encodedNumerals.Select(x => x.IntegralIndices.Select(y => Identity[y])).SelectMany(x => x).ToArray();
            return output;
        }

        /// <summary>
        /// Encodes the specified byte array into a string using the current numeral system and identity.
        /// </summary>
        /// <param name="bytes">The byte array to encode into a string.</param>
        /// <returns>A string representation of the encoded byte array, utilizing the specified identity mapping.</returns>
        public string EncodeToString(byte[] bytes)
        {
            var encoded = Encode(bytes);
            var output = encoded.Select(x => x.ToString()).ToArray();
            return string.Concat(output);
        }

        /// <summary>
        /// Encodes a given string to its encoded representation in string format.
        /// </summary>
        /// <param name="value">The string value to be encoded.</param>
        /// <returns>A string containing the encoded representation of the input string.</returns>
        public string EncodeToString(string value)
        {
            var encoded = Encode(value);
            var output = encoded.Select(x => x.ToString()).ToArray();
            return string.Concat(output);
        }

        /// <summary>
        /// Decodes an encoded string to its original string representation using the defined numeral system and identity list.
        /// </summary>
        /// <param name="encoded">The encoded string to be decoded.</param>
        /// <returns>The decoded string reconstructed from the encoded input.</returns>
        public string DecodeToString(string encoded)
        {
            var identityStringList = Identity.Select(x => x.ToString()).ToList();
            var split = encoded.SplitAndKeep(identityStringList.ToArray());
            var indices = split.Select(x => identityStringList.IndexOf(x)).ToList();
            var encodedNumeral = new Numeral(NumeralSystem, indices, new List<int>());
            var result = encodedNumeral.To(_stringSystem);
            var output = result.IntegralIndices.Select(x => Identity[x]).ToArray();
            return string.Concat(output);
        }

        /// <summary>
        /// Decodes an array of elements of type <typeparamref name="TBase"/> into a string representation
        /// using a specified numeral system and identity mapping.
        /// </summary>
        /// <param name="encoded">An array of encoded elements of type <typeparamref name="TBase"/> to be decoded into a string.</param>
        /// <returns>Returns a string decoded from the specified array of encoded elements.</returns>
        public string DecodeToString(TBase[] encoded)
        {
            var identityList = Identity.ToList();
            var indices = encoded.Select(x => identityList.IndexOf(x)).ToArray();
            var indicesGrouped = indices.Group(Size);
            var encodedNumerals = indicesGrouped.Select(x => new Numeral(NumeralSystem, x.ToList(), new List<int>())).ToList();
            var decodedNumerals = encodedNumerals.Select(x => x.To(_stringSystem)).ToArray();
            return string.Concat(decodedNumerals.Select(x => x.Char));
        }

        /// <summary>
        /// Decodes an array of encoded elements of base type TBase into a byte array.
        /// </summary>
        /// <param name="encoded">An array of encoded elements of base type TBase to be decoded.</param>
        /// <returns>A byte array representing the decoded values.</returns>
        public byte[] DecodeToBytes(TBase[] encoded)
        {
            var identityList = Identity.ToList();
            var indices = encoded.Select(x => identityList.IndexOf(x)).ToArray();
            var indicesGrouped = indices.Group(Size);
            var encodedNumerals = indicesGrouped.Select(x => new Numeral(NumeralSystem, x.ToList(), new List<int>())).ToList();
            var decodedNumerals = encodedNumerals.Select(x => x.To(_stringSystem)).ToArray();
            var groupedBytes = decodedNumerals.Select(x => x.Bytes);
            var removedTrailingZeros = groupedBytes.Select(x => RemoveTrailing(x, (byte)0)).ToArray();
            return removedTrailingZeros.SelectMany(x => x).ToArray();
        }

        /// <summary>
        /// Removes trailing elements from the provided array that match the specified value.
        /// </summary>
        /// <typeparam name="T">The type of elements in the input array.</typeparam>
        /// <param name="input">The array from which trailing elements matching the specified value should be removed.</param>
        /// <param name="value">The value to be removed from the end of the array.</param>
        /// <returns>A new array with trailing elements removed that match the specified value.</returns>
        private static T[] RemoveTrailing<T>(T[] input, T value)
        {
            if (input.Length == 0)
                return Array.Empty<T>();

            // Find the last index that does not match the given value
            int lastIndex = input.Length - 1;
            while (lastIndex >= 0 && EqualityComparer<T>.Default.Equals(input[lastIndex], value))
            {
                lastIndex--;
            }

            // Return the trimmed array
            return input.Take(lastIndex + 1).ToArray();
        }

        /// Adjusts the size of a numeral's integral part to a specified size by padding with zeros if necessary.
        /// <param name="numeral">The numeral to be adjusted.</param>
        /// <param name="size">The desired size of the numeral's integral part.</param>
        /// <return>A Numeral instance with the integral part adjusted to the specified size.</return>
        private static Numeral AdjustSize(Numeral numeral, int size)
        {
            if (numeral.IntegralIndices.Count == size) return numeral;
            var integral = numeral.IntegralIndices.ToList();
            var difference = size - numeral.IntegralIndices.Count;
            if (difference > 0)
            {
                integral = Enumerable.Repeat(0, difference).Concat(integral).ToList();
            }
            return new Numeral(numeral.Base, integral, numeral.FractionalIndices, numeral.Positive);
        }

        /// <summary>
        /// Represents a mathematical base or numeral system for encoding and decoding data.
        /// </summary>
        /// <typeparam name="TBase">
        /// The type of the elements in the base, which must implement IComparable, IComparable&lt;T&gt;,
        /// IConvertible, and IEquatable&lt;T&gt;.
        /// </typeparam>
        public Base(HashSet<TBase> identity)
        {
            if (null == identity) throw new ArgumentException("Identity cannot be null.");
            if (identity.Count == 0) throw new ArgumentException("Identity cannot be empty.");
            Identity = identity.ToList();
            NumeralSystem = Numeral.System.OfBase(Identity.Count);
            Size = NumeralSystem.Size;
        }
        
    }
}