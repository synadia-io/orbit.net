// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.RegularExpressions;

namespace Synadia.Orbit.ParameterizedSubject;

/// <summary>
/// Extension methods for creating safe parameterized NATS subjects.
/// </summary>
public static class ParameterizedSubjectExtensions
{
    // Characters that must be URL-encoded when appearing in subject tokens
    private static readonly HashSet<char> CharsRequiringEscape = [' ', '\t', '\r', '\n', '>', '*', '.', '%'];

    // Regex to detect invalid control characters in subject template
    private static readonly Regex InvalidSubjectChars = new(@"[\s\r\n]", RegexOptions.Compiled);

    /// <summary>
    /// Parameterizes a NATS subject by replacing '?' placeholders with sanitized values.
    /// Example: "user.login.?.event.?".Parameterize("john", "click") → "user.login.john.event.click".
    /// </summary>
    /// <param name="subjectTemplate">The subject template containing '?' placeholders.</param>
    /// <param name="parameters">Values to replace each '?' in order.</param>
    /// <returns>A safe, valid NATS subject.</returns>
    /// <exception cref="ArgumentNullException">If subjectTemplate or parameters is null.</exception>
    /// <exception cref="ArgumentException">If subjectTemplate contains \r or \n, or parameter count doesn't match placeholder count.</exception>
    public static string Parameterize(this string subjectTemplate, params string?[] parameters)
    {
#if NETSTANDARD
        if (subjectTemplate == null)
        {
            throw new ArgumentNullException(nameof(subjectTemplate));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }
#else
        ArgumentNullException.ThrowIfNull(subjectTemplate);
        ArgumentNullException.ThrowIfNull(parameters);
#endif

        // Validate subject template has no \s, \r or \n
        if (InvalidSubjectChars.IsMatch(subjectTemplate))
        {
            throw new ArgumentException("Subject template cannot contain space (\\s), carriage return (\\r) or line feed (\\n) characters.", nameof(subjectTemplate));
        }

        // Count number of '?' placeholders
        int placeholderCount = subjectTemplate.Count(c => c == '?');

        if (placeholderCount != parameters.Length)
        {
            throw new ArgumentException($"Subject template contains {placeholderCount} placeholder(s), but {parameters.Length} parameter(s) were provided.");
        }

        if (placeholderCount == 0)
        {
            return subjectTemplate; // No parameterization needed
        }

        var resultParts = new List<string>();
        int paramIndex = 0;
        int start = 0;

        // Split and replace each '?'
        for (int i = 0; i < subjectTemplate.Length; i++)
        {
            if (subjectTemplate[i] == '?')
            {
                // Add part before the '?'
                if (i > start)
                {
                    resultParts.Add(subjectTemplate.Substring(start, i - start));
                }

                // Sanitize the parameter: URL-encode unsafe chars
                string sanitizedParam = SanitizeParameter(parameters[paramIndex++]);

                resultParts.Add(sanitizedParam);
                start = i + 1;
            }
        }

        // Add remaining part after last '?'
        if (start < subjectTemplate.Length)
        {
            resultParts.Add(subjectTemplate.Substring(start));
        }

        return string.Join(string.Empty, resultParts);
    }

    /// <summary>
    /// Ensures the provided value contains none of the disallowed whitespace characters for NATS subject tokens:
    /// space, tab (\t), carriage return (\r), or line feed (\n).
    /// Throws if any are present.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">If the value contains space, \t, \r, or \n.</exception>
    public static void EnsureSanitized(this string? value)
    {
#if NETSTANDARD
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentNullException.ThrowIfNull(value);
#endif

        // Only check for these characters explicitly as requested: space, tab, CR, LF
        if (value.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("Value cannot contain space (\\s), tab (\\t), carriage return (\\r), or line feed (\\n) characters.", nameof(value));
        }
    }

    /// <summary>
    /// Sanitizes a parameter value by URL-encoding characters that could break subject syntax
    /// or be used for injection: space, \t, \r, \n, >, *, full-stop, %.
    /// </summary>
    private static string SanitizeParameter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // Fast path: if no dangerous chars, return as-is
        if (!value.Any(c => CharsRequiringEscape.Contains(c)))
        {
            return value ?? string.Empty;
        }

        // This ensures tokens remain readable when possible, but safe
        var encoded = new StringBuilder();
        foreach (char c in value ?? string.Empty)
        {
            if (CharsRequiringEscape.Contains(c))
            {
                encoded.Append('%');
                encoded.Append(((int)c).ToString("X2"));
            }
            else
            {
                encoded.Append(c);
            }
        }

        return encoded.ToString();
    }
}
