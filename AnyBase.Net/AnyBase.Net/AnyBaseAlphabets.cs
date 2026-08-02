using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AnyBase.Net
{
    /// <summary>
    /// Provides commonly used ordered character alphabets.
    /// </summary>
    /// <remarks>
    /// Base32 and Base64 entries use the RFC 4648 symbol order. AnyBase.Net still
    /// applies its configured byte encoding strategy; selecting an alphabet alone
    /// does not change that strategy into RFC 4648 bit packing.
    /// </remarks>
    public static class AnyBaseAlphabets
    {
        /// <summary>The binary alphabet.</summary>
        public const string Binary = "01";

        /// <summary>The octal alphabet.</summary>
        public const string Octal = "01234567";

        /// <summary>The decimal alphabet.</summary>
        public const string Decimal = "0123456789";

        /// <summary>The uppercase hexadecimal alphabet.</summary>
        public const string Hexadecimal = "0123456789ABCDEF";

        /// <summary>The RFC 4648 Base32 alphabet, without padding.</summary>
        public const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>The RFC 4648 Base64 alphabet, without padding.</summary>
        public const string Base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        /// <summary>The RFC 4648 URL-safe Base64 alphabet, without padding.</summary>
        public const string Base64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        private static readonly IReadOnlyDictionary<string, string> Presets =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["binary"] = Binary,
                    ["octal"] = Octal,
                    ["decimal"] = Decimal,
                    ["hex"] = Hexadecimal,
                    ["base32"] = Base32,
                    ["base64"] = Base64,
                    ["base64url"] = Base64Url
                });

        /// <summary>
        /// Gets the canonical preset names and their ordered alphabets.
        /// </summary>
        public static IReadOnlyDictionary<string, string> All => Presets;

        /// <summary>
        /// Tries to resolve a canonical preset name without case sensitivity.
        /// </summary>
        /// <param name="name">The preset name.</param>
        /// <param name="alphabet">The resolved ordered alphabet, or an empty string when not found.</param>
        /// <returns><see langword="true"/> when the preset exists.</returns>
        public static bool TryGet(string name, out string alphabet)
        {
            if (name == null)
            {
                alphabet = string.Empty;
                return false;
            }

            if (Presets.TryGetValue(name, out var resolved))
            {
                alphabet = resolved;
                return true;
            }

            alphabet = string.Empty;
            return false;
        }
    }
}
