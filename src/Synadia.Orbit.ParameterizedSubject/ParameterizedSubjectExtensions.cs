// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if NETSTANDARD2_0
using System.Text;
#else
using System.Buffers;
#endif

namespace Synadia.Orbit.ParameterizedSubject;

/// <summary>
/// Extension methods for creating safe parameterized NATS subjects.
/// </summary>
public static class ParameterizedSubjectExtensions
{
#if !NETSTANDARD2_0
    private const int StackAllocThreshold = 256;
#endif

#if NET9_0_OR_GREATER
    /// <summary>
    /// Parameterizes a NATS subject by replacing '?' placeholders with sanitized values.
    /// Example: "user.login.?.event.?".Parameterize("john", "click") → "user.login.john.event.click".
    /// </summary>
    /// <param name="subjectTemplate">The subject template containing '?' placeholders.</param>
    /// <param name="parameters">Values to replace each '?' in order.</param>
    /// <returns>A safe, valid NATS subject.</returns>
    /// <exception cref="ArgumentNullException">If subjectTemplate is null.</exception>
    /// <exception cref="ArgumentException">If subjectTemplate contains whitespace, or parameter count doesn't match placeholder count.</exception>
    public static string Parameterize(this string subjectTemplate, params ReadOnlySpan<string?> parameters)
    {
        ArgumentNullException.ThrowIfNull(subjectTemplate);

        if (ValidateAndCountPlaceholders(subjectTemplate, parameters.Length) == 0)
        {
            return subjectTemplate;
        }

        return ParameterizeCore(subjectTemplate, parameters);
    }

    /// <summary>
    /// Parameterizes a NATS subject by replacing '?' placeholders with sanitized values.
    /// Example: "user.login.?.event.?".Parameterize("john", "click") → "user.login.john.event.click".
    /// </summary>
    /// <param name="subjectTemplate">The subject template containing '?' placeholders.</param>
    /// <param name="parameters">Values to replace each '?' in order.</param>
    /// <returns>A safe, valid NATS subject.</returns>
    /// <exception cref="ArgumentNullException">If subjectTemplate or parameters is null.</exception>
    /// <exception cref="ArgumentException">If subjectTemplate contains whitespace, or parameter count doesn't match placeholder count.</exception>
    public static string Parameterize(this string subjectTemplate, params string?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(subjectTemplate);
        ArgumentNullException.ThrowIfNull(parameters);

        if (ValidateAndCountPlaceholders(subjectTemplate, parameters.Length) == 0)
        {
            return subjectTemplate;
        }

        return ParameterizeCore(subjectTemplate, (ReadOnlySpan<string?>)parameters);
    }
#elif NETSTANDARD2_0
    /// <summary>
    /// Parameterizes a NATS subject by replacing '?' placeholders with sanitized values.
    /// Example: "user.login.?.event.?".Parameterize("john", "click") → "user.login.john.event.click".
    /// </summary>
    /// <param name="subjectTemplate">The subject template containing '?' placeholders.</param>
    /// <param name="parameters">Values to replace each '?' in order.</param>
    /// <returns>A safe, valid NATS subject.</returns>
    /// <exception cref="ArgumentNullException">If subjectTemplate or parameters is null.</exception>
    /// <exception cref="ArgumentException">If subjectTemplate contains whitespace, or parameter count doesn't match placeholder count.</exception>
    public static string Parameterize(this string subjectTemplate, params string?[] parameters)
    {
        if (subjectTemplate == null)
        {
            throw new ArgumentNullException(nameof(subjectTemplate));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (ValidateAndCountPlaceholders(subjectTemplate, parameters.Length) == 0)
        {
            return subjectTemplate;
        }

        return ParameterizeSb(subjectTemplate, parameters);
    }
#else
    /// <summary>
    /// Parameterizes a NATS subject by replacing '?' placeholders with sanitized values.
    /// Example: "user.login.?.event.?".Parameterize("john", "click") → "user.login.john.event.click".
    /// </summary>
    /// <param name="subjectTemplate">The subject template containing '?' placeholders.</param>
    /// <param name="parameters">Values to replace each '?' in order.</param>
    /// <returns>A safe, valid NATS subject.</returns>
    /// <exception cref="ArgumentNullException">If subjectTemplate or parameters is null.</exception>
    /// <exception cref="ArgumentException">If subjectTemplate contains whitespace, or parameter count doesn't match placeholder count.</exception>
    public static string Parameterize(this string subjectTemplate, params string?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(subjectTemplate);
        ArgumentNullException.ThrowIfNull(parameters);

        if (ValidateAndCountPlaceholders(subjectTemplate, parameters.Length) == 0)
        {
            return subjectTemplate;
        }

        return ParameterizeCore(subjectTemplate, (ReadOnlySpan<string?>)parameters);
    }
#endif

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

        if (value.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("Value cannot contain space (\\s), tab (\\t), carriage return (\\r), or line feed (\\n) characters.", nameof(value));
        }
    }

    private static int ValidateAndCountPlaceholders(string subjectTemplate, int parameterCount)
    {
        if (subjectTemplate.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("Subject template cannot contain space (\\s), carriage return (\\r) or line feed (\\n) characters.", nameof(subjectTemplate));
        }

        int placeholderCount = 0;
        foreach (char t in subjectTemplate)
        {
            if (t == '?')
            {
                placeholderCount++;
            }
        }

        if (placeholderCount != parameterCount)
        {
            throw new ArgumentException($"Subject template contains {placeholderCount} placeholder(s), but {parameterCount} parameter(s) were provided.");
        }

        return placeholderCount;
    }

#if NETSTANDARD2_0
    private static string ParameterizeSb(string subjectTemplate, string?[] parameters)
    {
        var sb = new StringBuilder(subjectTemplate.Length);
        int paramIndex = 0;
        int start = 0;

        for (int i = 0; i < subjectTemplate.Length; i++)
        {
            if (subjectTemplate[i] == '?')
            {
                if (i > start)
                {
                    sb.Append(subjectTemplate, start, i - start);
                }

                AppendSanitizedSb(sb, parameters[paramIndex++]);
                start = i + 1;
            }
        }

        if (start < subjectTemplate.Length)
        {
            sb.Append(subjectTemplate, start, subjectTemplate.Length - start);
        }

        return sb.ToString();
    }

    private static void AppendSanitizedSb(StringBuilder sb, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        for (int i = 0; i < value!.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                case '>':
                case '*':
                case '.':
                case '%':
                    sb.Append('%');
                    sb.Append(((int)c).ToString("X2"));
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
    }
#else
    private static string ParameterizeCore(string subjectTemplate, ReadOnlySpan<string?> parameters)
    {
        int maxLen = subjectTemplate.Length;
        foreach (string? t in parameters)
        {
            maxLen += (t?.Length ?? 0) * 3;
        }

        char[]? rented = null;
        Span<char> buffer = maxLen <= StackAllocThreshold
            ? stackalloc char[StackAllocThreshold]
            : (rented = ArrayPool<char>.Shared.Rent(maxLen));

        try
        {
            int written = 0;
            int paramIndex = 0;
            int start = 0;

            for (int i = 0; i < subjectTemplate.Length; i++)
            {
                if (subjectTemplate[i] == '?')
                {
                    if (i > start)
                    {
                        subjectTemplate.AsSpan(start, i - start).CopyTo(buffer[written..]);
                        written += i - start;
                    }

                    written += WriteSanitized(buffer[written..], parameters[paramIndex++]);
                    start = i + 1;
                }
            }

            if (start < subjectTemplate.Length)
            {
                subjectTemplate.AsSpan(start).CopyTo(buffer[written..]);
                written += subjectTemplate.Length - start;
            }

            return new string(buffer[..written]);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static int WriteSanitized(Span<char> dest, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        int written = 0;
        foreach (char c in value)
        {
            switch (c)
            {
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                case '>':
                case '*':
                case '.':
                case '%':
                    dest[written++] = '%';
                    WriteTwoHexDigits(dest[written..], (byte)c);
                    written += 2;
                    break;
                default:
                    dest[written++] = c;
                    break;
            }
        }

        return written;
    }

    private static void WriteTwoHexDigits(Span<char> dest, byte value)
    {
        int hi = value >> 4;
        int lo = value & 0xF;
        dest[0] = (char)(hi < 10 ? '0' + hi : 'A' + hi - 10);
        dest[1] = (char)(lo < 10 ? '0' + lo : 'A' + lo - 10);
    }
#endif
}
