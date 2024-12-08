using System;
using System.Collections.Generic;

namespace AnyBase.Net
{
    /// <summary>
    /// Represents a generic interface for encoding and decoding byte arrays and strings using a specified identity type.
    /// </summary>
    /// <typeparam name="TBase">
    /// The type of the identity used for encoding and decoding. This type must implement
    /// <see cref="IComparable"/>, <see cref="IComparable{TBase}"/>, <see cref="IConvertible"/>,
    /// and <see cref="IEquatable{TBase}"/> interfaces.
    /// </typeparam>
    public interface IBase<TBase> where TBase: IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
    {
        /// <summary>
        /// Gets the identity set used by this base system. The identity is an
        /// ordered, read-only list of type <typeparamref name="TBase"/> that
        /// represents the distinct set of symbols or values used in encoding
        /// and decoding operations within the numeral system.
        /// </summary>
        /// <remarks>
        /// The identity set defines the unique symbols or values that make up the base
        /// of the numeral system in this implementation. Each element must be unique
        /// and the number of elements determines the base size of the numeral system.
        /// </remarks>
        public IReadOnlyList<TBase> Identity { get; }

        /// <summary>
        /// Encodes a byte array into an array of type <typeparamref name="TBase"/> using the specified numeral system.
        /// </summary>
        /// <param name="bytes">The byte array to be encoded.</param>
        /// <returns>An array of type <typeparamref name="TBase"/> representing the encoded byte array.</returns>
        public TBase[] Encode(byte[] bytes);

        /// <summary>
        /// Encodes a byte array into an array of elements of type <typeparamref name="TBase"/>.
        /// </summary>
        /// <param name="bytes">The byte array to be encoded.</param>
        /// <returns>An array of <typeparamref name="TBase"/> representing the encoded data.</returns>
        public TBase[] Encode(string bytes);

        /// <summary>
        /// Encodes the given byte array into a string representation based on the specified base identity.
        /// </summary>
        /// <param name="bytes">The byte array to encode into a string.</param>
        /// <returns>A string representation of the encoded byte array.</returns>
        public string EncodeToString(byte[] bytes);

        /// <summary>
        /// Encodes the provided input string into a string representation
        /// using the defined encoding scheme based on the identity list.
        /// </summary>
        /// <param name="value">The string input to be encoded.</param>
        /// <returns>A string that represents the encoded form of the input string.</returns>
        public string EncodeToString(string value);

        /// Decodes the given encoded string to its original string representation using the numeral system and identity list.
        /// <param name="encoded">The encoded string to be decoded.</param>
        /// <returns>The original string representation of the encoded input.</returns>
        public string DecodeToString(string encoded);

        /// Decodes an array of encoded symbols into a string representation based on the specified numeral system and identity list.
        /// <param name="encoded">An array of encoded symbols representing the original string data.</param>
        /// <returns>The decoded string representation from the encoded symbols.</returns>
        public string DecodeToString(TBase[] encoded);

        /// Decodes an array of encoded data elements into an array of bytes.
        /// <param name="encoded">An array of encoded elements representing the data to be decoded.</param>
        /// <return>An array of bytes obtained from the decoded data.</return>
        public byte[] DecodeToBytes(TBase[] bytes);
        
 
    }
}