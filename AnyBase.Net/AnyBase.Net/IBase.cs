using System;
using System.Collections.Generic;

namespace AnyBase.Net
{
    /// <summary>
    /// Encodes and decodes bytes and UTF-8 text using an ordered identity.
    /// </summary>
    /// <typeparam name="TBase">The symbol type used by the identity.</typeparam>
    public interface IBase<TBase>
        where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
    {
        /// <summary>
        /// Gets the symbols in numeric order, where the first symbol represents zero.
        /// </summary>
        IReadOnlyList<TBase> Identity { get; }

        /// <summary>
        /// Encodes bytes into identity symbols.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <returns>The encoded identity symbols.</returns>
        TBase[] Encode(byte[] bytes);

        /// <summary>
        /// Encodes a string as UTF-8 bytes and then as identity symbols.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The encoded identity symbols.</returns>
        TBase[] Encode(string value);

        /// <summary>
        /// Encodes bytes and concatenates the text representation of their identity symbols.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <returns>The concatenated encoded symbols.</returns>
        string EncodeToString(byte[] bytes);

        /// <summary>
        /// Encodes a string as UTF-8 and concatenates its identity symbols.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The concatenated encoded symbols.</returns>
        string EncodeToString(string value);

        /// <summary>
        /// Parses concatenated identity symbols and decodes the resulting UTF-8 bytes.
        /// </summary>
        /// <param name="encoded">The concatenated encoded symbols.</param>
        /// <returns>The decoded string.</returns>
        string DecodeToString(string encoded);

        /// <summary>
        /// Decodes identity symbols as UTF-8 bytes.
        /// </summary>
        /// <param name="encoded">The encoded identity symbols.</param>
        /// <returns>The decoded string.</returns>
        string DecodeToString(TBase[] encoded);

        /// <summary>
        /// Decodes identity symbols into bytes.
        /// </summary>
        /// <param name="encoded">The encoded identity symbols.</param>
        /// <returns>The decoded bytes.</returns>
        byte[] DecodeToBytes(TBase[] encoded);
    }
}
