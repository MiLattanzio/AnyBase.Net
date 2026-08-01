using System;
using System.Collections.Generic;
using System.Text;

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
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
        TBase[] Encode(byte[] bytes);

        /// <summary>
        /// Encodes a string as UTF-8 bytes and then as identity symbols.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The encoded identity symbols.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="EncoderFallbackException"><paramref name="value"/> is not valid UTF-16.</exception>
        TBase[] Encode(string value);

        /// <summary>
        /// Encodes bytes and concatenates the text representation of their identity symbols.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <returns>The concatenated encoded symbols.</returns>
        /// <remarks>
        /// String operations require non-empty, unique, prefix-free symbol text. Use
        /// <see cref="Encode(byte[])"/> when the alphabet is not prefix-free.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The alphabet cannot be represented unambiguously as concatenated text.
        /// </exception>
        string EncodeToString(byte[] bytes);

        /// <summary>
        /// Encodes a string as UTF-8 and concatenates its identity symbols.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The concatenated encoded symbols.</returns>
        /// <remarks>
        /// String operations require non-empty, unique, prefix-free symbol text. Use
        /// <see cref="Encode(string)"/> when the alphabet is not prefix-free.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="EncoderFallbackException"><paramref name="value"/> is not valid UTF-16.</exception>
        /// <exception cref="InvalidOperationException">
        /// The alphabet cannot be represented unambiguously as concatenated text.
        /// </exception>
        string EncodeToString(string value);

        /// <summary>
        /// Parses concatenated identity symbols and decodes the resulting UTF-8 bytes.
        /// </summary>
        /// <param name="encoded">The concatenated encoded symbols.</param>
        /// <returns>The decoded string.</returns>
        /// <remarks>
        /// String operations require non-empty, unique, prefix-free symbol text. For other
        /// alphabets, tokenize the input externally and use <see cref="DecodeToString(TBase[])"/>.
        /// Text positions reported in errors are zero-based UTF-16 indices.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="encoded"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The alphabet cannot be represented unambiguously as concatenated text.
        /// </exception>
        /// <exception cref="FormatException">
        /// The input contains unknown symbol text, has an incomplete byte group, or represents a value above 255.
        /// </exception>
        /// <exception cref="DecoderFallbackException">The decoded bytes are not valid UTF-8.</exception>
        string DecodeToString(string encoded);

        /// <summary>
        /// Decodes identity symbols as UTF-8 bytes.
        /// </summary>
        /// <param name="encoded">The encoded identity symbols.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="encoded"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">
        /// The input contains an unknown symbol, has an incomplete byte group, or represents a value above 255.
        /// </exception>
        /// <exception cref="DecoderFallbackException">The decoded bytes are not valid UTF-8.</exception>
        string DecodeToString(TBase[] encoded);

        /// <summary>
        /// Decodes identity symbols into bytes.
        /// </summary>
        /// <param name="encoded">The encoded identity symbols.</param>
        /// <returns>The decoded bytes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="encoded"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">
        /// The input contains an unknown symbol, has an incomplete byte group, or represents a value above 255.
        /// </exception>
        byte[] DecodeToBytes(TBase[] encoded);
    }
}
