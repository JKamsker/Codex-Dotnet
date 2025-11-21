using Codex.DotNet.Public.Models;
using FluentAssertions;

namespace Codex.DotNet.Tests.Unit;

/// <summary>
/// Unit tests for the SessionId value object.
/// </summary>
public class SessionIdTests
{
    [Fact]
    public void Parse_ValidString_CreatesSessionId()
    {
        // Arrange
        var validId = "session-123-abc";

        // Act
        var sessionId = SessionId.Parse(validId);

        // Assert
        sessionId.Value.Should().Be(validId);
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var emptyId = "";

        // Act
        var act = () => SessionId.Parse(emptyId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_WhitespaceString_ThrowsArgumentException(string? invalidId)
    {
        // Act
        var act = () => SessionId.Parse(invalidId!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value");
    }

    [Fact]
    public void TryParse_ValidString_ReturnsTrue()
    {
        // Arrange
        var validId = "session-456-def";

        // Act
        var result = SessionId.TryParse(validId, out var sessionId);

        // Assert
        result.Should().BeTrue();
        sessionId.Value.Should().Be(validId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TryParse_InvalidString_ReturnsFalse(string? invalidId)
    {
        // Act
        var result = SessionId.TryParse(invalidId, out var sessionId);

        // Assert
        result.Should().BeFalse();
        sessionId.Should().Be(default(SessionId));
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        // Arrange
        var idString = "session-789-ghi";

        // Act
        SessionId sessionId = idString;

        // Assert
        sessionId.Value.Should().Be(idString);
    }

    [Fact]
    public void ImplicitConversion_ToString_Works()
    {
        // Arrange
        var sessionId = SessionId.Parse("session-abc-123");

        // Act
        string result = sessionId;

        // Assert
        result.Should().Be("session-abc-123");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var id = "session-equal-test";
        var sessionId1 = SessionId.Parse(id);
        var sessionId2 = SessionId.Parse(id);

        // Assert
        sessionId1.Should().Be(sessionId2);
        (sessionId1 == sessionId2).Should().BeTrue();
        sessionId1.GetHashCode().Should().Be(sessionId2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var sessionId1 = SessionId.Parse("session-1");
        var sessionId2 = SessionId.Parse("session-2");

        // Assert
        sessionId1.Should().NotBe(sessionId2);
        (sessionId1 == sessionId2).Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var idString = "session-tostring-test";
        var sessionId = SessionId.Parse(idString);

        // Act
        var result = sessionId.ToString();

        // Assert
        result.Should().Be(idString);
    }
}
