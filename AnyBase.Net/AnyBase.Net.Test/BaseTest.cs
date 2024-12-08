using System.Text;

namespace AnyBase.Net.Test;

/// The BaseTest class contains unit tests for encoding and decoding operations
/// across various numeral systems such as hexadecimal, binary, octal, and decimal.
/// Each test verifies the ability of the system to accurately encode a string
/// into the specified base notation and then decode it back to the original string.
public class BaseTest
{
    /// <summary>
    /// Represents a binary numeral system base with two possible values: '0' and '1'.
    /// This variable is used for encoding and decoding data to and from binary format.
    /// </summary>
    private static readonly Base<char> Binary = new Base<char>(new HashSet<char>(new[]
    {
        '0', '1'
    }));

    /// <summary>
    /// Represents an octal numeral system (base-8) using a set of characters ranging from '0' to '7'.
    /// </summary>
    /// <remarks>
    /// The Octal variable is an instance of the <see cref="Base{char}"/> class, initialized with the characters '0' through '7',
    /// which are used as digits in the octal numeral system.
    /// </remarks>
    private static readonly Base<char> Octal = new Base<char>(new HashSet<char>(new[]
    {
        '0', '1', '2', '3', '4', '5', '6', '7'
    }));

    /// <summary>
    /// Represents a base-10 numeral system using characters '0' to '9'.
    /// </summary>
    /// <remarks>
    /// This instance of <see cref="Base{TBase}"/> is specifically tailored to encode and
    /// decode data using a decimal numeral system, which is commonly used in everyday
    /// arithmetic and counting. The characters constituting this numeral system include
    /// digits from '0' to '9'.
    /// </remarks>
    private static readonly Base<char> Decimal = new Base<char>(new HashSet<char>(new[]
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
    }));

    /// <summary>
    /// Represents a hexadecimal numeral system for encoding and decoding operations.
    /// </summary>
    /// <remarks>
    /// The Hex variable is an instance of the Base class, parameterized with char,
    /// and initialized with a character set consisting of the digits 0-9 and
    /// letters A-F, representing the hexadecimal numeral system. It provides
    /// methods for encoding strings and byte arrays to hexadecimal representations
    /// and decoding hexadecimal representations back to strings or byte arrays.
    /// </remarks>
    private static readonly Base<char> Hex = new Base<char>(new HashSet<char>(new[]
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'A', 'B', 'C', 'D', 'E', 'F',
    }));


    /// <summary>
    /// Tests the hexadecimal encoding and decoding functionality of the Base class.
    /// </summary>
    /// <remarks>
    /// This test verifies that the encoding of a string to hexadecimal using the Base class produces the expected result,
    /// and that decoding the hexadecimal back to a string retrieves the original string. It also verifies encoding and decoding using byte arrays.
    /// </remarks>
    [Test]
    public void HexTest()
    {
        var value = "Plant trees";
        var chars = Hex.Encode(value);
        var charString = string.Join("", chars);
        var charStr = Hex.EncodeToString(value);
        Assert.That(charString, Is.EqualTo(charStr));
        var valueBytes = Encoding.ASCII.GetBytes(value);
        var charsFromBytes = Hex.Encode(valueBytes);
        var charsFromString = string.Join("", charsFromBytes);
        Assert.That(charStr, Is.EqualTo(charsFromString));
        var decodedString = Hex.DecodeToString(chars);
        Assert.That(value, Is.EqualTo(decodedString));
        var decodedBytes = Hex.DecodeToBytes(charsFromBytes);
        var utf8DecodedString = Encoding.ASCII.GetString(decodedBytes);
        Assert.That(value, Is.EqualTo(utf8DecodedString));
    }

    /// <summary>
    /// Tests the encoding and decoding functionality of the Binary base system using the ASCII encoded string "Plant trees".
    /// </summary>
    /// <remarks>
    /// This test verifies that encoding a string into a binary representation and then decoding it back results in the original string.
    /// It checks encoding to both characters and a string format, and ensures both give the same result.
    /// It also tests encoding and decoding of ASCII byte arrays to confirm byte-level accuracy.
    /// </remarks>
    [Test]
    public void BinaryTest()
    {
        var value = "Plant trees";
        var chars = Binary.Encode(value);
        var charString = string.Join("", chars);
        var charStr = Binary.EncodeToString(value);
        Assert.That(charString, Is.EqualTo(charStr));
        var valueBytes = Encoding.ASCII.GetBytes(value);
        var charsFromBytes = Binary.Encode(valueBytes);
        var charsFromString = string.Join("", charsFromBytes);
        Assert.That(charStr, Is.EqualTo(charsFromString));
        var decodedString = Binary.DecodeToString(chars);
        Assert.That(value, Is.EqualTo(decodedString));
        var decodedBytes = Binary.DecodeToBytes(charsFromBytes);
        var utf8DecodedString = Encoding.ASCII.GetString(decodedBytes);
        Assert.That(value, Is.EqualTo(utf8DecodedString));
    }

    /// <summary>
    /// Tests the functionality of encoding and decoding operations in an octal numeral system.
    /// </summary>
    /// <remarks>
    /// This test verifies the encoding of a string and binary data into octal representation
    /// and ensures accurate decoding back to the original string and byte data.
    /// It performs a series of assertions to confirm the fidelity of the encode-decode process.
    /// </remarks>
    [Test]
    public void OctalTest()
    {
        var value = "Plant trees";
        var chars = Octal.Encode(value);
        var charString = string.Join("", chars);
        var charStr = Octal.EncodeToString(value);
        Assert.That(charString, Is.EqualTo(charStr));
        var valueBytes = Encoding.ASCII.GetBytes(value);
        var charsFromBytes = Octal.Encode(valueBytes);
        var charsFromString = string.Join("", charsFromBytes);
        Assert.That(charStr, Is.EqualTo(charsFromString));
        var decodedString = Octal.DecodeToString(chars);
        Assert.That(value, Is.EqualTo(decodedString));
        var decodedBytes = Octal.DecodeToBytes(charsFromBytes);
        var utf8DecodedString = Encoding.ASCII.GetString(decodedBytes);
        Assert.That(value, Is.EqualTo(utf8DecodedString));
    }

    /// Tests the encoding and decoding functionalities of the decimal base conversion.
    /// Validates the consistency and correctness of encoding a string to a character array
    /// and a byte array, ensuring the encoded values can be appropriately decoded back to
    /// the original string. It checks the operations through assertions to confirm the
    /// output is as expected across different conversion methods.
    [Test]
    public void DecimalTest()
    {
        var value = "Plant trees";
        var chars = Decimal.Encode(value);
        var charString = string.Join("", chars);
        var charStr = Decimal.EncodeToString(value);
        Assert.That(charString, Is.EqualTo(charStr));
        var valueBytes = Encoding.ASCII.GetBytes(value);
        var charsFromBytes = Decimal.Encode(valueBytes);
        var charsFromString = string.Join("", charsFromBytes);
        Assert.That(charStr, Is.EqualTo(charsFromString));
        var decodedString = Decimal.DecodeToString(chars);
        Assert.That(value, Is.EqualTo(decodedString));
        var decodedBytes = Decimal.DecodeToBytes(charsFromBytes);
        var utf8DecodedString = Encoding.ASCII.GetString(decodedBytes);
        Assert.That(value, Is.EqualTo(utf8DecodedString));
    }
    
}