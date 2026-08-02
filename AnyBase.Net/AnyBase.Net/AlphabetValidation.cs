using System;
using System.Collections.Generic;
using System.Linq;

namespace AnyBase.Net
{
    /// <summary>
    /// Identifies a validation problem in an ordered alphabet.
    /// </summary>
    public enum AlphabetValidationDiagnosticKind
    {
        /// <summary>The alphabet enumerable is null.</summary>
        NullAlphabet,

        /// <summary>The alphabet contains fewer than two symbols.</summary>
        TooFewSymbols,

        /// <summary>A symbol is null.</summary>
        NullSymbol,

        /// <summary>Two symbols are equal according to the configured comparer.</summary>
        DuplicateSymbol,

        /// <summary>A symbol has an empty text representation.</summary>
        EmptyTextRepresentation,

        /// <summary>Two symbols have the same text representation.</summary>
        DuplicateTextRepresentation,

        /// <summary>One symbol text is a prefix of another.</summary>
        PrefixCollision,

        /// <summary>The requested separator is null or empty.</summary>
        EmptySeparator,

        /// <summary>The requested separator occurs inside a symbol text.</summary>
        SeparatorCollision
    }

    /// <summary>
    /// Describes one alphabet validation problem and the symbols involved.
    /// </summary>
    public sealed class AlphabetValidationDiagnostic
    {
        internal AlphabetValidationDiagnostic(
            AlphabetValidationDiagnosticKind kind,
            string message,
            bool textOperationsOnly,
            int? symbolIndex = null,
            int? conflictingSymbolIndex = null)
        {
            Kind = kind;
            Message = message;
            TextOperationsOnly = textOperationsOnly;
            SymbolIndex = symbolIndex;
            ConflictingSymbolIndex = conflictingSymbolIndex;
        }

        /// <summary>Gets the problem category.</summary>
        public AlphabetValidationDiagnosticKind Kind { get; }

        /// <summary>Gets a detailed, human-readable description.</summary>
        public string Message { get; }

        /// <summary>
        /// Gets whether the problem affects only text operations while symbol-array operations remain valid.
        /// </summary>
        public bool TextOperationsOnly { get; }

        /// <summary>Gets the zero-based symbol index involved, when available.</summary>
        public int? SymbolIndex { get; }

        /// <summary>Gets the zero-based index of the conflicting symbol, when available.</summary>
        public int? ConflictingSymbolIndex { get; }
    }

    /// <summary>
    /// Contains structural and textual validation information for an alphabet.
    /// </summary>
    public sealed class AlphabetValidationResult
    {
        internal AlphabetValidationResult(IReadOnlyList<AlphabetValidationDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Gets whether the alphabet can be used by symbol-array operations.
        /// </summary>
        public bool IsValid => Diagnostics.All(diagnostic => diagnostic.TextOperationsOnly);

        /// <summary>
        /// Gets whether the alphabet can be used by the text operation being validated.
        /// </summary>
        public bool IsTextCompatible => Diagnostics.Count == 0;

        /// <summary>Gets all detected problems.</summary>
        public IReadOnlyList<AlphabetValidationDiagnostic> Diagnostics { get; }
    }

    /// <summary>
    /// Validates ordered alphabets without constructing a codec.
    /// </summary>
    public static class AlphabetValidator
    {
        /// <summary>
        /// Validates an alphabet for symbol-array and concatenated-text operations.
        /// </summary>
        /// <typeparam name="TBase">The symbol type.</typeparam>
        /// <param name="alphabet">The ordered alphabet.</param>
        /// <param name="comparer">An optional comparer used for symbol uniqueness.</param>
        /// <returns>Detailed structural and text compatibility diagnostics.</returns>
        public static AlphabetValidationResult Validate<TBase>(
            IEnumerable<TBase>? alphabet,
            IEqualityComparer<TBase>? comparer = null)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            if (alphabet == null)
            {
                return Result(new AlphabetValidationDiagnostic(
                    AlphabetValidationDiagnosticKind.NullAlphabet,
                    "Alphabet cannot be null.",
                    textOperationsOnly: false));
            }

            return ValidateMaterialized(
                alphabet.ToList(),
                comparer ?? EqualityComparer<TBase>.Default,
                separator: null,
                useSeparator: false);
        }

        /// <summary>
        /// Validates an alphabet for symbol-array operations and text operations using a separator.
        /// </summary>
        /// <typeparam name="TBase">The symbol type.</typeparam>
        /// <param name="alphabet">The ordered alphabet.</param>
        /// <param name="separator">The exact separator placed between symbol texts.</param>
        /// <param name="comparer">An optional comparer used for symbol uniqueness.</param>
        /// <returns>Detailed structural, text, and separator diagnostics.</returns>
        public static AlphabetValidationResult ValidateWithSeparator<TBase>(
            IEnumerable<TBase>? alphabet,
            string? separator,
            IEqualityComparer<TBase>? comparer = null)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            if (alphabet == null)
            {
                return Result(new AlphabetValidationDiagnostic(
                    AlphabetValidationDiagnosticKind.NullAlphabet,
                    "Alphabet cannot be null.",
                    textOperationsOnly: false));
            }

            return ValidateMaterialized(
                alphabet.ToList(),
                comparer ?? EqualityComparer<TBase>.Default,
                separator,
                useSeparator: true);
        }

        internal static AlphabetValidationResult ValidateMaterialized<TBase>(
            IReadOnlyList<TBase> alphabet,
            IEqualityComparer<TBase> comparer,
            string? separator,
            bool useSeparator)
            where TBase : IComparable, IComparable<TBase>, IConvertible, IEquatable<TBase>
        {
            var diagnostics = new List<AlphabetValidationDiagnostic>();

            if (alphabet.Count < 2)
            {
                diagnostics.Add(new AlphabetValidationDiagnostic(
                    AlphabetValidationDiagnosticKind.TooFewSymbols,
                    $"Alphabet must contain at least two symbols; found {alphabet.Count}.",
                    textOperationsOnly: false));
            }

            for (var index = 0; index < alphabet.Count; index++)
            {
                if (alphabet[index] is null)
                {
                    diagnostics.Add(new AlphabetValidationDiagnostic(
                        AlphabetValidationDiagnosticKind.NullSymbol,
                        $"Alphabet symbol at index {index} is null.",
                        textOperationsOnly: false,
                        symbolIndex: index));
                }
            }

            for (var firstIndex = 0; firstIndex < alphabet.Count; firstIndex++)
            {
                if (alphabet[firstIndex] is null)
                {
                    continue;
                }

                for (var secondIndex = firstIndex + 1; secondIndex < alphabet.Count; secondIndex++)
                {
                    if (alphabet[secondIndex] is null)
                    {
                        continue;
                    }

                    if (comparer.Equals(alphabet[firstIndex], alphabet[secondIndex]))
                    {
                        diagnostics.Add(new AlphabetValidationDiagnostic(
                            AlphabetValidationDiagnosticKind.DuplicateSymbol,
                            $"Alphabet symbols at indices {firstIndex} and {secondIndex} are equal according to the configured comparer.",
                            textOperationsOnly: false,
                            symbolIndex: firstIndex,
                            conflictingSymbolIndex: secondIndex));
                    }
                }
            }

            if (diagnostics.Any(diagnostic => !diagnostic.TextOperationsOnly))
            {
                return Result(diagnostics);
            }

            var texts = alphabet.Select(SymbolText).ToArray();
            for (var index = 0; index < texts.Length; index++)
            {
                if (texts[index].Length == 0)
                {
                    diagnostics.Add(new AlphabetValidationDiagnostic(
                        AlphabetValidationDiagnosticKind.EmptyTextRepresentation,
                        $"Identity symbol at index {index} has an empty text representation. String encoding and decoding require non-empty symbol text; use the symbol-array APIs instead.",
                        textOperationsOnly: true,
                        symbolIndex: index));
                }
            }

            for (var firstIndex = 0; firstIndex < texts.Length; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < texts.Length; secondIndex++)
                {
                    if (string.Equals(texts[firstIndex], texts[secondIndex], StringComparison.Ordinal))
                    {
                        diagnostics.Add(new AlphabetValidationDiagnostic(
                            AlphabetValidationDiagnosticKind.DuplicateTextRepresentation,
                            $"Identity symbols at indices {firstIndex} and {secondIndex} both use the text '{EscapeForMessage(texts[firstIndex])}'. String encoding and decoding require unique symbol text; use the symbol-array APIs instead.",
                            textOperationsOnly: true,
                            symbolIndex: firstIndex,
                            conflictingSymbolIndex: secondIndex));
                    }
                }
            }

            if (useSeparator)
            {
                if (string.IsNullOrEmpty(separator))
                {
                    diagnostics.Add(new AlphabetValidationDiagnostic(
                        AlphabetValidationDiagnosticKind.EmptySeparator,
                        "Separator must contain at least one character.",
                        textOperationsOnly: true));
                }
                else
                {
                    for (var index = 0; index < texts.Length; index++)
                    {
                        if (texts[index].IndexOf(separator, StringComparison.Ordinal) >= 0)
                        {
                            diagnostics.Add(new AlphabetValidationDiagnostic(
                                AlphabetValidationDiagnosticKind.SeparatorCollision,
                                $"Separator '{EscapeForMessage(separator)}' occurs inside identity symbol text '{EscapeForMessage(texts[index])}' at index {index}.",
                                textOperationsOnly: true,
                                symbolIndex: index));
                        }
                    }
                }
            }
            else
            {
                for (var firstIndex = 0; firstIndex < texts.Length; firstIndex++)
                {
                    for (var secondIndex = firstIndex + 1; secondIndex < texts.Length; secondIndex++)
                    {
                        if (texts[firstIndex].Length == 0 || texts[secondIndex].Length == 0 ||
                            string.Equals(texts[firstIndex], texts[secondIndex], StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var firstIsShorter = texts[firstIndex].Length < texts[secondIndex].Length;
                        var shorterIndex = firstIsShorter ? firstIndex : secondIndex;
                        var longerIndex = firstIsShorter ? secondIndex : firstIndex;
                        if (texts[longerIndex].StartsWith(texts[shorterIndex], StringComparison.Ordinal))
                        {
                            diagnostics.Add(new AlphabetValidationDiagnostic(
                                AlphabetValidationDiagnosticKind.PrefixCollision,
                                $"Identity symbol text '{EscapeForMessage(texts[shorterIndex])}' at index {shorterIndex} is a prefix of '{EscapeForMessage(texts[longerIndex])}' at index {longerIndex}. String encoding and decoding require a prefix-free textual alphabet; use the symbol-array APIs instead.",
                                textOperationsOnly: true,
                                symbolIndex: shorterIndex,
                                conflictingSymbolIndex: longerIndex));
                        }
                    }
                }
            }

            return Result(diagnostics);
        }

        internal static string SymbolText<TBase>(TBase symbol)
        {
            return symbol is null ? string.Empty : symbol.ToString() ?? string.Empty;
        }

        internal static string EscapeForMessage(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0");
        }

        private static AlphabetValidationResult Result(params AlphabetValidationDiagnostic[] diagnostics)
        {
            return Result((IEnumerable<AlphabetValidationDiagnostic>)diagnostics);
        }

        private static AlphabetValidationResult Result(IEnumerable<AlphabetValidationDiagnostic> diagnostics)
        {
            return new AlphabetValidationResult(Array.AsReadOnly(diagnostics.ToArray()));
        }
    }
}
