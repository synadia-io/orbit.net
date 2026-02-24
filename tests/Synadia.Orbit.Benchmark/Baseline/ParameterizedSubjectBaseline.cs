// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Snapshot of ParameterizedSubjectExtensions taken before optimizations.
// Used as the baseline for benchmark comparisons.
using System.Text;

namespace Synadia.Orbit.Benchmark.Baseline;

internal static class ParameterizedSubjectBaseline
{
    private static readonly HashSet<char> CharsRequiringEscape = [' ', '\t', '\r', '\n', '>', '*', '.', '%'];

    internal static string Parameterize(string subjectTemplate, params string?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(subjectTemplate);
        ArgumentNullException.ThrowIfNull(parameters);

        if (subjectTemplate.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("Subject template cannot contain whitespace characters.", nameof(subjectTemplate));
        }

        int placeholderCount = subjectTemplate.Count(c => c == '?');

        if (placeholderCount != parameters.Length)
        {
            throw new ArgumentException($"Subject template contains {placeholderCount} placeholder(s), but {parameters.Length} parameter(s) were provided.");
        }

        if (placeholderCount == 0)
        {
            return subjectTemplate;
        }

        var resultParts = new List<string>();
        int paramIndex = 0;
        int start = 0;

        for (int i = 0; i < subjectTemplate.Length; i++)
        {
            if (subjectTemplate[i] == '?')
            {
                if (i > start)
                {
                    resultParts.Add(subjectTemplate.Substring(start, i - start));
                }

                string sanitizedParam = SanitizeParameter(parameters[paramIndex++]);

                resultParts.Add(sanitizedParam);
                start = i + 1;
            }
        }

        if (start < subjectTemplate.Length)
        {
            resultParts.Add(subjectTemplate.Substring(start));
        }

        return string.Join(string.Empty, resultParts);
    }

    internal static void EnsureSanitized(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("Value cannot contain whitespace characters.", nameof(value));
        }
    }

    private static string SanitizeParameter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        if (!value.Any(c => CharsRequiringEscape.Contains(c)))
        {
            return value ?? string.Empty;
        }

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
