namespace AnyBase.Net.Test;

public class AlphabetApiTest
{
    [Test]
    public void Catalog_ContainsCanonicalRfcOrderedPresets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AnyBaseAlphabets.All.Keys, Is.EquivalentTo(new[]
            {
                "binary", "octal", "decimal", "hex", "base32", "base64", "base64url"
            }));
            Assert.That(AnyBaseAlphabets.Base32, Is.EqualTo("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
            Assert.That(
                AnyBaseAlphabets.Base64,
                Is.EqualTo("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"));
            Assert.That(
                AnyBaseAlphabets.Base64Url,
                Is.EqualTo("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"));
            Assert.That(AnyBaseAlphabets.TryGet("BASE64URL", out var alphabet), Is.True);
            Assert.That(alphabet, Is.EqualTo(AnyBaseAlphabets.Base64Url));
        });
    }

    [Test]
    public void Factories_CreateExpectedAlphabets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AnyBase.CreateBinary().Identity, Is.EqualTo(AnyBaseAlphabets.Binary));
            Assert.That(AnyBase.CreateOctal().Identity, Is.EqualTo(AnyBaseAlphabets.Octal));
            Assert.That(AnyBase.CreateDecimal().Identity, Is.EqualTo(AnyBaseAlphabets.Decimal));
            Assert.That(AnyBase.CreateHex().Identity, Is.EqualTo(AnyBaseAlphabets.Hexadecimal));
            Assert.That(AnyBase.CreateBase32().Identity, Is.EqualTo(AnyBaseAlphabets.Base32));
            Assert.That(AnyBase.CreateBase64().Identity, Is.EqualTo(AnyBaseAlphabets.Base64));
            Assert.That(AnyBase.CreateBase64Url().Identity, Is.EqualTo(AnyBaseAlphabets.Base64Url));
            Assert.That(AnyBase.Create("ZA").Identity, Is.EqualTo("ZA"));
        });
    }

    [Test]
    public void Validator_ReportsStructuralAndTextDiagnostics()
    {
        var duplicate = AlphabetValidator.Validate("010");
        var ambiguous = AlphabetValidator.Validate(new[] { "a", "ab" });

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.Diagnostics[0].Kind, Is.EqualTo(AlphabetValidationDiagnosticKind.DuplicateSymbol));
            Assert.That(duplicate.Diagnostics[0].SymbolIndex, Is.EqualTo(0));
            Assert.That(duplicate.Diagnostics[0].ConflictingSymbolIndex, Is.EqualTo(2));
            Assert.That(ambiguous.IsValid, Is.True);
            Assert.That(ambiguous.IsTextCompatible, Is.False);
            Assert.That(ambiguous.Diagnostics[0].Kind, Is.EqualTo(AlphabetValidationDiagnosticKind.PrefixCollision));
            Assert.That(ambiguous.Diagnostics[0].SymbolIndex, Is.EqualTo(0));
            Assert.That(ambiguous.Diagnostics[0].ConflictingSymbolIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public void Validator_SeparatorMakesPrefixAlphabetTextCompatible()
    {
        var valid = AlphabetValidator.ValidateWithSeparator(new[] { "a", "ab" }, "|");
        var collision = AlphabetValidator.ValidateWithSeparator(new[] { "a", "ab" }, "b");

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.IsTextCompatible, Is.True);
            Assert.That(valid.Diagnostics, Is.Empty);
            Assert.That(collision.IsValid, Is.True);
            Assert.That(collision.IsTextCompatible, Is.False);
            Assert.That(collision.Diagnostics.Single().Kind, Is.EqualTo(AlphabetValidationDiagnosticKind.SeparatorCollision));
        });
    }

    [Test]
    public void Separator_RoundTripsPrefixAmbiguousSymbols()
    {
        var sut = new Base<string>(new[] { "a", "ab" });

        var encoded = sut.EncodeToString("A", "|");

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Is.EqualTo("a|ab|a|a|a|a|a|ab"));
            Assert.That(sut.DecodeToString(encoded, "|"), Is.EqualTo("A"));
            Assert.That(() => sut.DecodeToString(encoded), Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => sut.EncodeToString("A", "a"),
                Throws.TypeOf<ArgumentException>().With.Message.Contains("occurs inside"));
        });
    }

    [Test]
    public void Separator_RoundTripsWhenItOverlapsASymbolBoundary()
    {
        var sut = new Base<string>(new[] { "a", "b" });

        var encoded = sut.EncodeToString("A", "aa");

        Assert.Multiple(() =>
        {
            Assert.That(
                encoded,
                Is.EqualTo(string.Join("aa", new[] { "a", "b", "a", "a", "a", "a", "a", "b" })));
            Assert.That(sut.DecodeToString(encoded, "aa"), Is.EqualTo("A"));
        });
    }

    [Test]
    public void CustomComparer_IsUsedForSymbolLookup()
    {
        var comparer = CharComparer.OrdinalIgnoreCase;
        var sut = new Base<char>("AB", comparer);
        var encoded = sut.Encode(new byte[] { 65 });
        var lowerCase = new string(encoded).ToLowerInvariant().ToCharArray();

        Assert.Multiple(() =>
        {
            Assert.That(sut.Comparer, Is.SameAs(comparer));
            Assert.That(sut.DecodeToBytes(lowerCase), Is.EqualTo(new byte[] { 65 }));
            Assert.That(
                AlphabetValidator.Validate("Aa", comparer).Diagnostics.Single().Kind,
                Is.EqualTo(AlphabetValidationDiagnosticKind.DuplicateSymbol));
        });
    }

    [Test]
    public void TryDecode_ReturnsFalseAndEmptyOutputForMalformedInput()
    {
        var sut = AnyBase.CreateHex();
        IBase<char> contract = sut;

        Assert.Multiple(() =>
        {
            Assert.That(sut.TryDecodeToBytes("41", out var bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(new byte[] { 65 }));
            Assert.That(sut.TryDecodeToBytes("4Z", out var invalidBytes), Is.False);
            Assert.That(invalidBytes, Is.Empty);
            Assert.That(contract.TryDecodeToString("41", out var text), Is.True);
            Assert.That(text, Is.EqualTo("A"));
            Assert.That(contract.TryDecodeToString("4Z", out var invalidText), Is.False);
            Assert.That(invalidText, Is.Empty);
            Assert.That(sut.TryDecodeToString("4-1", "-", out var separated), Is.True);
            Assert.That(separated, Is.EqualTo("A"));
        });
    }

    private sealed class CharComparer : IEqualityComparer<char>
    {
        public static CharComparer OrdinalIgnoreCase { get; } = new();

        public bool Equals(char x, char y)
        {
            return char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
        }

        public int GetHashCode(char obj)
        {
            return char.ToUpperInvariant(obj).GetHashCode();
        }
    }
}
