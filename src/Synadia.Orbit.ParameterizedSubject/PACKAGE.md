# Synadia.Orbit.ParameterizedSubject [EXPERIMENTAL]

> **This package is experimental.** It is not part of the official NATS ecosystem and is published
> exclusively in Orbit .NET to gauge interest and address community requests. The API may change
> or the package may be removed in a future release.

Small, dependency-free helpers for building safe, parameterized NATS subjects.

This package provides the extension method `string ToNatsSubject(params string[] parameters)` that replaces `?` placeholders in a subject template with sanitized values so they are always valid NATS subject tokens.

## Why

NATS subjects forbid whitespace and certain characters in tokens. When subjects are composed from dynamic input (user IDs, versions, etc.), you must sanitize values to prevent invalid subjects or injection via wildcard characters like `*` and `>`.

`ParameterizedSubjectExtensions.ToNatsSubject` makes this simple and consistent:

- Replace each `?` in the template with a value you supply
- Sanitize unsafe characters by percent-encoding them
- Validate the template does not contain whitespace/CR/LF
- Ensure the number of placeholders matches the number of provided parameters

## Installation

Install from NuGet:

```
dotnet add package Synadia.Orbit.ParameterizedSubject
```

Target frameworks: `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, `net10.0`.

## Quick start

```csharp
using Synadia.Orbit.ParameterizedSubject;

var subject = "users.?.events.?".ToNatsSubject("alice", "login");
// => "users.alice.events.login"
```

Adjacent placeholders are allowed and simply concatenate:

```csharp
var s = "pre.??.post".ToNatsSubject("A", "B");
// => "pre.AB.post"
```

## Placeholder rules

- Use `?` as a placeholder for a single parameter.
- The number of `?` characters in the template must equal the number of provided parameters.
- Leading/trailing and consecutive placeholders are supported.
- A parameter that is `null` or `string.Empty` becomes an empty token.

If the counts do not match, an `ArgumentException` is thrown with a message indicating the mismatch.

## Sanitization

To keep subjects valid and prevent wildcard injection, the following characters are percent-encoded when they appear in parameter values:

- Space ` ` → `%20`
- Tab `\t` → `%09`
- Carriage return `\r` → `%0D`
- Line feed `\n` → `%0A`
- Full stop `.` → `%2E`
- Asterisk `*` → `%2A`
- Greater-than `>` → `%3E`
- Percent `%` → `%25`

Example:

```csharp
var s = "files.?".ToNatsSubject("v1.2");
// => "files.v1%2E2"

var s2 = "a.?".ToNatsSubject("x*y>z%");
// => "a.x%2Ay%3Ez%25"
```

## Template validation

Subject templates themselves must not contain whitespace or newlines. If the template contains space, tab, `\r`, or `\n`, the method throws an `ArgumentException`:

```csharp
"a b.?".ToNatsSubject("x"); // throws
```

## API

```csharp
namespace Synadia.Orbit.ParameterizedSubject
{
    public static class ParameterizedSubjectExtensions
    {
        // Replaces '?' in subjectTemplate with sanitized parameters in order.
        public static string ToNatsSubject(this string subjectTemplate, params string[] parameters);

        // Validates a value contains no space, tab (\t), carriage return (\r), or line feed (\n).
        // Throws ArgumentNullException when value is null; ArgumentException when any of those characters are present.
        public static void EnsureValidNatsSubject(this string value);
    }
}
```

Exceptions:
- `ArgumentNullException` when `subjectTemplate` or `parameters` is `null` (framework-dependent guard).
- `ArgumentException` when the template contains whitespace/CR/LF.
- `ArgumentException` when placeholder and parameter counts differ.

For `EnsureValidNatsSubject` specifically:
- `ArgumentNullException` when `value` is `null`.
- `ArgumentException` when `value` contains space, `\t`, `\r`, or `\n`.

### EnsureValidNatsSubject usage

Use `EnsureValidNatsSubject` when you only need to validate inputs (without transforming them) to guarantee they are safe as NATS subject tokens with respect to whitespace:

```csharp
using Synadia.Orbit.ParameterizedSubject;

"ok".EnsureValidNatsSubject();           // no throw
"v1.2".EnsureValidNatsSubject();        // no throw
"has space".EnsureValidNatsSubject();   // throws ArgumentException
```

## Algorithm

`ToNatsSubject` processes the template in a single left-to-right pass:

1. **Validate template** — reject if it contains any whitespace (`space`, `\t`, `\r`, `\n`).
2. **Count placeholders** — count `?` characters in the template. Reject if the count does not match the number of supplied parameters. If zero, return the template unchanged.
3. **Build result** — walk the template character by character:
   - Copy literal characters (non-`?`) to the output.
   - For each `?`, consume the next parameter and append its characters to the output, percent-encoding unsafe characters as `%XX` (two uppercase hex digits).
4. **Return** the assembled string.

### Encoded characters

| Character | Reason | Encoding |
|-----------|--------|----------|
| `%` | Escape character itself (encode first to avoid double-encoding) | `%25` |
| `.` | NATS token separator | `%2E` |
| `*` | NATS wildcard (single token) | `%2A` |
| `>` | NATS wildcard (tail match) | `%3E` |
| ` ` | Invalid in NATS subjects | `%20` |
| `\t` | Invalid in NATS subjects | `%09` |
| `\r` | Invalid in NATS subjects | `%0D` |
| `\n` | Invalid in NATS subjects | `%0A` |

All other characters pass through unmodified.

## Performance

`ToNatsSubject` is designed to add safety with minimal allocation overhead. On modern .NET targets, the only heap allocation is the output string itself — matching raw string interpolation.

Key optimizations by target framework:

| Target | Optimization |
|--------|-------------|
| `net9.0`+ | `params ReadOnlySpan<string?>` eliminates the `params` array allocation |
| `net8.0`+ | `SearchValues<char>` for SIMD-accelerated whitespace detection |
| `netstandard2.1`+, `net8.0`+ | `stackalloc` + `Span<char>` with `ArrayPool` fallback for zero-alloc string building |
| `netstandard2.0` | `StringBuilder`-based fallback for broad compatibility |

### vs. manual sanitization

Compared to hand-written sanitization using chained `.Replace()` calls, `ToNatsSubject` is faster while producing **identical allocations** to raw (unsafe) interpolation:

```
| Method                              | Mean       | Allocated |
|------------------------------------ |-----------:|----------:|
| Single_Interpolation_Unsafe         |   6.70 ns  |      56 B |
| Single_Interpolation_ManualSanitize |  48.10 ns  |      56 B |
| Single_ToNatsSubject                |  44.01 ns  |      56 B |
|                                     |            |           |
| Multi_Interpolation_Unsafe          |  26.03 ns  |      72 B |
| Multi_Interpolation_ManualSanitize  | 155.33 ns  |      72 B |
| Multi_ToNatsSubject                 |  71.76 ns  |      72 B |
```

Raw interpolation is naturally the fastest since it performs no validation or encoding — but it's also **unsafe** for dynamic input (no protection against wildcard injection or invalid subjects). `ToNatsSubject` is consistently faster than the manual `.Replace()` chain approach (up to 2x for multiple parameters) while adding template validation and correct percent-encoding.

## Versioning and license

This project follows semantic versioning. See `LICENSE` at the repo root for the Apache 2.0 license.
