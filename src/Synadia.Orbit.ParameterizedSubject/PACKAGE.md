# Synadia.Orbit.ParameterizedSubject

Small, dependency-free helpers for building safe, parameterized NATS subjects.

This package provides the extension method `string Parameterize(params string[] parameters)` that replaces `?` placeholders in a subject template with sanitized values so they are always valid NATS subject tokens.

## Why

NATS subjects forbid whitespace and certain characters in tokens. When subjects are composed from dynamic input (user IDs, versions, etc.), you must sanitize values to prevent invalid subjects or injection via wildcard characters like `*` and `>`.

`ParameterizedSubjectExtensions.Parameterize` makes this simple and consistent:

- Replace each `?` in the template with a value you supply
- Sanitize unsafe characters by percent-encoding them
- Validate the template does not contain whitespace/CR/LF
- Ensure the number of placeholders matches the number of provided parameters

## Installation

Install from NuGet:

```
dotnet add package Synadia.Orbit.ParameterizedSubject
```

Target frameworks: `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`.

## Quick start

```csharp
using Synadia.Orbit.ParameterizedSubject;

var subject = "users.?.events.?".Parameterize("alice", "login");
// => "users.alice.events.login"
```

Adjacent placeholders are allowed and simply concatenate:

```csharp
var s = "pre.??.post".Parameterize("A", "B");
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
var s = "files.?".Parameterize("v1.2");
// => "files.v1%2E2"

var s2 = "a.?".Parameterize("x*y>z%");
// => "a.x%2Ay%3Ez%25"
```

## Template validation

Subject templates themselves must not contain whitespace or newlines. If the template matches `\s`, `\r`, or `\n`, the method throws an `ArgumentException`:

```csharp
"a b.?".Parameterize("x"); // throws
```

## API

```csharp
namespace Synadia.Orbit.ParameterizedSubject
{
    public static class ParameterizedSubjectExtensions
    {
        // Replaces '?' in subjectTemplate with sanitized parameters in order.
        public static string Parameterize(this string subjectTemplate, params string[] parameters);
    }
}
```

Exceptions:
- `ArgumentNullException` when `subjectTemplate` or `parameters` is `null` (framework-dependent guard).
- `ArgumentException` when the template contains whitespace/CR/LF.
- `ArgumentException` when placeholder and parameter counts differ.

## Testing

The repository includes an xUnit test suite that exercises:

- Basic replacement (single/multiple, leading/trailing, consecutive placeholders)
- Mismatch count errors and validation messages
- Template whitespace validation
- Parameter sanitization for all encoded characters
- Null and empty parameter handling

## Versioning and license

This project follows semantic versioning. See `LICENSE` at the repo root for the Apache 2.0 license.

## Notes

- The method is allocation-conscious and only encodes when necessary.
- Encoding preserves readability where possible while guaranteeing subject safety.
