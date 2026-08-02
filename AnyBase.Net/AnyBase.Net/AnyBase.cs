using System;
using System.Collections.Generic;

namespace AnyBase.Net
{
    /// <summary>
    /// Creates AnyBase.Net codecs from custom or predefined alphabets.
    /// </summary>
    public static class AnyBase
    {
        /// <summary>Creates a character codec from an ordered alphabet.</summary>
        /// <param name="alphabet">The ordered alphabet.</param>
        /// <returns>A codec using <paramref name="alphabet"/>.</returns>
        public static Base<char> Create(string alphabet)
        {
            return new Base<char>(alphabet);
        }

        /// <summary>Creates a codec from an ordered alphabet.</summary>
        /// <typeparam name="TBase">The symbol type.</typeparam>
        /// <param name="alphabet">The ordered alphabet.</param>
        /// <returns>A codec using <paramref name="alphabet"/>.</returns>
        public static Base<TBase> Create<TBase>(IEnumerable<TBase> alphabet)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            return new Base<TBase>(alphabet);
        }

        /// <summary>Creates a codec using a custom symbol comparer.</summary>
        /// <typeparam name="TBase">The symbol type.</typeparam>
        /// <param name="alphabet">The ordered alphabet.</param>
        /// <param name="comparer">The comparer used for uniqueness and symbol lookup.</param>
        /// <returns>A codec using <paramref name="alphabet"/> and <paramref name="comparer"/>.</returns>
        public static Base<TBase> Create<TBase>(
            IEnumerable<TBase> alphabet,
            IEqualityComparer<TBase> comparer)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            return new Base<TBase>(alphabet, comparer);
        }

        /// <summary>Creates a binary codec.</summary>
        public static Base<char> CreateBinary() => Create(AnyBaseAlphabets.Binary);

        /// <summary>Creates an octal codec.</summary>
        public static Base<char> CreateOctal() => Create(AnyBaseAlphabets.Octal);

        /// <summary>Creates a decimal codec.</summary>
        public static Base<char> CreateDecimal() => Create(AnyBaseAlphabets.Decimal);

        /// <summary>Creates an uppercase hexadecimal codec.</summary>
        public static Base<char> CreateHex() => Create(AnyBaseAlphabets.Hexadecimal);

        /// <summary>Creates a codec using the RFC 4648 Base32 alphabet.</summary>
        public static Base<char> CreateBase32() => Create(AnyBaseAlphabets.Base32);

        /// <summary>Creates a codec using the RFC 4648 Base64 alphabet.</summary>
        public static Base<char> CreateBase64() => Create(AnyBaseAlphabets.Base64);

        /// <summary>Creates a codec using the RFC 4648 URL-safe Base64 alphabet.</summary>
        public static Base<char> CreateBase64Url() => Create(AnyBaseAlphabets.Base64Url);
    }
}
