using NCodexSDK.Public.Models;
using FluentAssertions;

namespace NCodexSDK.Tests.Unit;

/// <summary>
/// Unit tests for the CodexReasoningEffort value object.
/// </summary>
public class CodexReasoningEffortTests
{
    [Fact]
    public void Predefined_Low_HasCorrectValue()
    {
        // Act
        var effort = CodexReasoningEffort.Low;

        // Assert
        effort.Value.Should().Be("low");
    }

    [Fact]
    public void Predefined_Medium_HasCorrectValue()
    {
        // Act
        var effort = CodexReasoningEffort.Medium;

        // Assert
        effort.Value.Should().Be("medium");
    }

    [Fact]
    public void Predefined_High_HasCorrectValue()
    {
        // Act
        var effort = CodexReasoningEffort.High;

        // Assert
        effort.Value.Should().Be("high");
    }

    [Fact]
    public void Predefined_XHigh_HasCorrectValue()
    {
        // Act
        var effort = CodexReasoningEffort.XHigh;

        // Assert
        effort.Value.Should().Be("xhigh");
    }

    [Fact]
    public void Parse_CustomValue_Works()
    {
        // Arrange
        var customValue = "ultra-high";

        // Act
        var effort = CodexReasoningEffort.Parse(customValue);

        // Assert
        effort.Value.Should().Be(customValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_InvalidValue_ThrowsArgumentException(string? invalidValue)
    {
        // Act
        var act = () => CodexReasoningEffort.Parse(invalidValue!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void TryParse_ValidValue_ReturnsTrue()
    {
        // Arrange
        var validValue = "custom-effort";

        // Act
        var result = CodexReasoningEffort.TryParse(validValue, out var effort);

        // Assert
        result.Should().BeTrue();
        effort.Value.Should().Be(validValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_InvalidValue_ReturnsFalse(string? invalidValue)
    {
        // Act
        var result = CodexReasoningEffort.TryParse(invalidValue, out var effort);

        // Assert
        result.Should().BeFalse();
        effort.Should().Be(default(CodexReasoningEffort));
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        // Arrange
        var effortString = "custom";

        // Act
        CodexReasoningEffort effort = effortString;

        // Assert
        effort.Value.Should().Be(effortString);
    }

    [Fact]
    public void ImplicitConversion_ToString_Works()
    {
        // Arrange
        var effort = CodexReasoningEffort.XHigh;

        // Act
        string result = effort;

        // Assert
        result.Should().Be("xhigh");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var effortName = "equal-test";
        var effort1 = CodexReasoningEffort.Parse(effortName);
        var effort2 = CodexReasoningEffort.Parse(effortName);

        // Assert
        effort1.Should().Be(effort2);
        (effort1 == effort2).Should().BeTrue();
        effort1.GetHashCode().Should().Be(effort2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var effort1 = CodexReasoningEffort.Low;
        var effort2 = CodexReasoningEffort.High;

        // Assert
        effort1.Should().NotBe(effort2);
        (effort1 == effort2).Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var effortString = "tostring-test";
        var effort = CodexReasoningEffort.Parse(effortString);

        // Act
        var result = effort.ToString();

        // Assert
        result.Should().Be(effortString);
    }

    [Fact]
    public void PredefinedEfforts_AreDistinct()
    {
        // Act
        var low = CodexReasoningEffort.Low;
        var medium = CodexReasoningEffort.Medium;
        var high = CodexReasoningEffort.High;
        var xhigh = CodexReasoningEffort.XHigh;

        // Assert
        low.Should().NotBe(medium);
        medium.Should().NotBe(high);
        low.Should().NotBe(high);
        xhigh.Should().NotBe(high);
        xhigh.Should().NotBe(medium);
        xhigh.Should().NotBe(low);
    }

    [Fact]
    public void Parse_StandardValues_MatchPredefined()
    {
        // Act & Assert
        CodexReasoningEffort.Parse("low").Should().Be(CodexReasoningEffort.Low);
        CodexReasoningEffort.Parse("medium").Should().Be(CodexReasoningEffort.Medium);
        CodexReasoningEffort.Parse("high").Should().Be(CodexReasoningEffort.High);
        CodexReasoningEffort.Parse("xhigh").Should().Be(CodexReasoningEffort.XHigh);
    }
}
