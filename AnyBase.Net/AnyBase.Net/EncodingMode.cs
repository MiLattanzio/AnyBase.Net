namespace AnyBase.Net
{
    /// <summary>
    /// Selects how input bytes are mapped to alphabet symbols.
    /// </summary>
    public enum EncodingMode
    {
        /// <summary>
        /// Encodes every byte independently using the configured fixed symbol width.
        /// This is the historical AnyBase.Net behavior.
        /// </summary>
        FixedWidthByte = 0,

        /// <summary>
        /// Encodes the complete byte sequence as one most-significant-bit-first bit stream.
        /// The alphabet size must be a power of two between 2 and 256.
        /// </summary>
        Packed = 1
    }
}
