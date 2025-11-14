// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Synadia.Orbit.ParameterizedSubject;
using Xunit;

namespace Synadia.Orbit.ParameterizedSubject.Test;

public class ParameterizedSubjectExtensionsTests
{
    [Fact]
    public void Parameterize_ReplacesSinglePlaceholder()
    {
        var actual = "user.login.?".Parameterize("john");
        Assert.Equal("user.login.john", actual);
    }

    [Fact]
    public void Parameterize_ReplacesMultiplePlaceholders_InOrder()
    {
        var actual = "a.?.b.?".Parameterize("x", "y");
        Assert.Equal("a.x.b.y", actual);
    }

    [Fact]
    public void Parameterize_NoPlaceholders_ReturnsTemplate()
    {
        var actual = "a.b.c".Parameterize();
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
        var actual = "a.?".Parameterize(input);
        Assert.Equal($"a.{expectedSanitized}", actual);
    }

    [Fact]
    public void Parameterize_NullParameter_BecomesEmptyToken()
    {
        // Passing a params array explicitly with a single null element
        var actual = "a.?.b".Parameterize(new string?[] { null }!);
        Assert.Equal("a..b", actual);
    }

    [Fact]
    public void Parameterize_EmptyParameter_BecomesEmptyToken()
    {
        var actual = "x.?".Parameterize(string.Empty);
        Assert.Equal("x.", actual);
    }

    [Fact]
    public void Parameterize_LeadingAndTrailingPlaceholders_Work()
    {
        var actual1 = "?.a".Parameterize("left");
        var actual2 = "a.?".Parameterize("right");
        Assert.Equal("left.a", actual1);
        Assert.Equal("a.right", actual2);
    }

    [Fact]
    public void Parameterize_ConsecutivePlaceholders_ConcatenateParameters()
    {
        var actual = "pre.??.post".Parameterize("A", "B");
        Assert.Equal("pre.AB.post", actual);
    }
}
