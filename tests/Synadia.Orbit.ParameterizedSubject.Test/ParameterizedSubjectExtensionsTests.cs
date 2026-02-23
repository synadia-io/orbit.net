// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.ParameterizedSubject.Test;

public class ParameterizedSubjectExtensionsTests
{
    [Fact]
    public void Parameterize_ReplacesSinglePlaceholder()
    {
        string actual = "user.login.?".Parameterize("john");
        Assert.Equal("user.login.john", actual);
    }

    [Fact]
    public void Parameterize_ReplacesMultiplePlaceholders_InOrder()
    {
        string actual = "a.?.b.?".Parameterize("x", "y");
        Assert.Equal("a.x.b.y", actual);
    }

    [Fact]
    public void Parameterize_NoPlaceholders_ReturnsTemplate()
    {
        string actual = "a.b.c".Parameterize();
        Assert.Equal("a.b.c", actual);
    }

    [Theory]
    [InlineData("a.b.c", new[] { "x" })]
    [InlineData("a.?", new string[] { })]
    [InlineData("a.?.b.?", new[] { "only-one" })]
    [InlineData("?.?", new[] { "one" })]
    public void Parameterize_MismatchPlaceholderCount_Throws(string template, string[] parameters)
    {
        var ex = Assert.Throws<ArgumentException>(() => template.Parameterize(parameters));
        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("a b.?")]
    [InlineData("a\tb.?")]
    [InlineData("a\r\n?")]
    public void Parameterize_TemplateWithWhitespace_Throws(string template)
    {
        var ex = Assert.Throws<ArgumentException>(() => template.Parameterize("x"));
        Assert.Contains("Subject template cannot contain", ex.Message);
    }

    [Theory]
    [InlineData("one two", "one%20two")]
    [InlineData("tab\there", "tab%09here")]
    [InlineData("line\r\nend", "line%0D%0Aend")]
    [InlineData("v1.2", "v1%2E2")]
    [InlineData("x*y>z%", "x%2Ay%3Ez%25")]
    public void Parameterize_SanitizesSpecialCharacters(string input, string expectedSanitized)
    {
        string actual = "a.?".Parameterize(input);
        Assert.Equal($"a.{expectedSanitized}", actual);
    }

    [Fact]
    public void Parameterize_NullParameter_BecomesEmptyToken()
    {
        // Passing a params array explicitly with a single null element
        string actual = "a.?.b".Parameterize([null]!);
        Assert.Equal("a..b", actual);
    }

    [Fact]
    public void Parameterize_EmptyParameter_BecomesEmptyToken()
    {
        string actual = "x.?".Parameterize(string.Empty);
        Assert.Equal("x.", actual);
    }

    [Fact]
    public void Parameterize_LeadingAndTrailingPlaceholders_Work()
    {
        string actual1 = "?.a".Parameterize("left");
        string actual2 = "a.?".Parameterize("right");
        Assert.Equal("left.a", actual1);
        Assert.Equal("a.right", actual2);
    }

    [Fact]
    public void Parameterize_ConsecutivePlaceholders_ConcatenateParameters()
    {
        string actual = "pre.??.post".Parameterize("A", "B");
        Assert.Equal("pre.AB.post", actual);
    }

    [Fact]
    public void Parameterize_NullTemplate_ThrowsArgumentNullException()
    {
        string? template = null;
        Assert.Throws<ArgumentNullException>(() => template!.Parameterize("x"));
    }

    [Fact]
    public void Parameterize_EntireTemplateIsSinglePlaceholder()
    {
        string actual = "?".Parameterize("value");
        Assert.Equal("value", actual);
    }

    [Fact]
    public void Parameterize_AllPlaceholdersNoLiteralText()
    {
        string actual = "?.?.?".Parameterize("a", "b", "c");
        Assert.Equal("a.b.c", actual);
    }

    [Fact]
    public void Parameterize_TwoConsecutivePlaceholdersOnly()
    {
        string actual = "??".Parameterize("x", "y");
        Assert.Equal("xy", actual);
    }

    [Theory]
    [InlineData(" ", "%20")]
    [InlineData("\t", "%09")]
    [InlineData("\r", "%0D")]
    [InlineData("\n", "%0A")]
    [InlineData(".", "%2E")]
    [InlineData("*", "%2A")]
    [InlineData(">", "%3E")]
    [InlineData("%", "%25")]
    public void Parameterize_EncodesEachSpecialCharacterIndividually(string input, string expected)
    {
        string actual = "a.?".Parameterize(input);
        Assert.Equal($"a.{expected}", actual);
    }

    [Fact]
    public void Parameterize_LongInput_ExceedsStackAllocThreshold()
    {
        var longParam = new string('x', 300);
        string actual = "prefix.?".Parameterize(longParam);
        Assert.Equal($"prefix.{longParam}", actual);
    }

    [Fact]
    public void Parameterize_LongInputRequiringEncoding_ExceedsStackAllocThreshold()
    {
        var longParam = new string('.', 100);
        var expectedEncoded = string.Concat(Enumerable.Repeat("%2E", 100));
        string actual = "prefix.?".Parameterize(longParam);
        Assert.Equal($"prefix.{expectedEncoded}", actual);
    }

    // === EnsureSanitized tests ===
    [Theory]
    [InlineData("abc")]
    [InlineData("v1.2")] // dot is allowed for EnsureSanitized (only checks for whitespace)
    [InlineData("*%>")] // wildcards/percent allowed here; only whitespace is disallowed
    public void EnsureSanitized_ValidInputs_DoNotThrow(string input)
    {
        var ex = Record.Exception(input.EnsureSanitized);
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a\tb")]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    public void EnsureSanitized_WithWhitespace_Throws(string input)
    {
        var ex = Assert.Throws<ArgumentException>(input.EnsureSanitized);
        Assert.Contains("Value cannot contain", ex.Message);
    }

    [Fact]
    public void EnsureSanitized_EmptyString_DoesNotThrow()
    {
        var ex = Record.Exception(string.Empty.EnsureSanitized);
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureSanitized_Null_ThrowsArgumentNullException()
    {
        string? s = null;
        Assert.Throws<ArgumentNullException>(() => s.EnsureSanitized());
    }
}
