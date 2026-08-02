using System;

namespace AnyBase.Net
{
    /// <summary>
    /// Provides non-throwing decoding helpers without adding members to <see cref="IBase{TBase}"/>.
    /// </summary>
    public static class BaseTryExtensions
    {
        /// <summary>Tries to decode identity symbols into bytes.</summary>
        public static bool TryDecodeToBytes<TBase>(
            this IBase<TBase> codec,
            TBase[] encoded,
            out byte[] bytes)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            EnsureCodec(codec);
            try
            {
                bytes = codec.DecodeToBytes(encoded);
                return true;
            }
            catch (Exception exception) when (IsDecodeFailure(exception))
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }

        /// <summary>Tries to decode identity symbols into UTF-8 text.</summary>
        public static bool TryDecodeToString<TBase>(
            this IBase<TBase> codec,
            TBase[] encoded,
            out string value)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            EnsureCodec(codec);
            try
            {
                value = codec.DecodeToString(encoded);
                return true;
            }
            catch (Exception exception) when (IsDecodeFailure(exception))
            {
                value = string.Empty;
                return false;
            }
        }

        /// <summary>Tries to decode concatenated identity symbols into UTF-8 text.</summary>
        public static bool TryDecodeToString<TBase>(
            this IBase<TBase> codec,
            string encoded,
            out string value)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            EnsureCodec(codec);
            try
            {
                value = codec.DecodeToString(encoded);
                return true;
            }
            catch (Exception exception) when (IsDecodeFailure(exception))
            {
                value = string.Empty;
                return false;
            }
        }

        /// <summary>Tries to decode concatenated identity symbols into bytes.</summary>
        public static bool TryDecodeToBytes<TBase>(
            this Base<TBase> codec,
            string encoded,
            out byte[] bytes)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            EnsureCodec(codec);
            try
            {
                bytes = codec.DecodeToBytes(encoded);
                return true;
            }
            catch (Exception exception) when (IsDecodeFailure(exception))
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }

        /// <summary>Tries to decode separated identity symbols into bytes.</summary>
        public static bool TryDecodeToBytes<TBase>(
            this Base<TBase> codec,
            string encoded,
            string separator,
            out byte[] bytes)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            EnsureCodec(codec);
            try
            {
                bytes = codec.DecodeToBytes(encoded, separator);
                return true;
            }
            catch (Exception exception) when (IsDecodeFailure(exception))
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }

        /// <summary>Tries to decode separated identity symbols into UTF-8 text.</summary>
        public static bool TryDecodeToString<TBase>(
            this Base<TBase> codec,
            string encoded,
            string separator,
            out string value)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            EnsureCodec(codec);
            try
            {
                value = codec.DecodeToString(encoded, separator);
                return true;
            }
            catch (Exception exception) when (IsDecodeFailure(exception))
            {
                value = string.Empty;
                return false;
            }
        }

        private static void EnsureCodec<TBase>(IBase<TBase> codec)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }
        }

        private static bool IsDecodeFailure(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is FormatException ||
                   exception is InvalidOperationException;
        }
    }
}
