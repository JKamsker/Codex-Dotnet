using NCodexSDK.Public.Models;
using FluentAssertions;

namespace NCodexSDK.Tests.Unit;

/// <summary>
/// Unit tests for the CodexModel value object.
/// </summary>
public class CodexModelTests
{
    [Fact]
    public void Default_ReturnsGpt51CodexMax()
    {
        // Act
        var defaultModel = CodexModel.Default;

        // Assert
        defaultModel.Value.Should().Be("gpt-5.1-codex-max");
    }

    [Fact]
    public void Parse_ValidModel_CreatesCodexModel()
    {
        // Arrange
        var modelName = "gpt-4-turbo";

        // Act
        var model = CodexModel.Parse(modelName);

        // Assert
        model.Value.Should().Be(modelName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_InvalidModel_ThrowsArgumentException(string? invalidModel)
    {
        // Act
        var act = () => CodexModel.Parse(invalidModel!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("*cannot be empty or whitespace*");
    }

    [Fact]
    public void Predefined_Gpt51CodexMax_HasCorrectValue()
    {
        // Act
        var model = CodexModel.Gpt51CodexMax;

        // Assert
        model.Value.Should().Be("gpt-5.1-codex-max");
    }

    [Fact]
    public void Predefined_Gpt51Codex_HasCorrectValue()
    {
        // Act
        var model = CodexModel.Gpt51Codex;

        // Assert
        model.Value.Should().Be("gpt-5.1-codex");
    }

    [Fact]
    public void Predefined_Gpt51CodexMini_HasCorrectValue()
    {
        // Act
        var model = CodexModel.Gpt51CodexMini;

        // Assert
        model.Value.Should().Be("gpt-5.1-codex-mini");
    }

    [Fact]
    public void Predefined_Gpt51_HasCorrectValue()
    {
        // Act
        var model = CodexModel.Gpt51;

        // Assert
        model.Value.Should().Be("gpt-5.1");
    }

    [Fact]
    public void TryParse_ValidModel_ReturnsTrue()
    {
        // Arrange
        var modelName = "custom-model-v1";

        // Act
        var result = CodexModel.TryParse(modelName, out var model);

        // Assert
        result.Should().BeTrue();
        model.Value.Should().Be(modelName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_InvalidModel_ReturnsFalse(string? invalidModel)
    {
        // Act
        var result = CodexModel.TryParse(invalidModel, out var model);

        // Assert
        result.Should().BeFalse();
        model.Should().Be(default(CodexModel));
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        // Arrange
        var modelString = "gpt-5-preview";

        // Act
        CodexModel model = modelString;

        // Assert
        model.Value.Should().Be(modelString);
    }

    [Fact]
    public void ImplicitConversion_ToString_Works()
    {
        // Arrange
        var model = CodexModel.Parse("gpt-5.1-codex-max");

        // Act
        string result = model;

        // Assert
        result.Should().Be("gpt-5.1-codex-max");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var modelName = "gpt-equality-test";
        var model1 = CodexModel.Parse(modelName);
        var model2 = CodexModel.Parse(modelName);

        // Assert
        model1.Should().Be(model2);
        (model1 == model2).Should().BeTrue();
        model1.GetHashCode().Should().Be(model2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var model1 = CodexModel.Gpt51Codex;
        var model2 = CodexModel.Gpt51CodexMini;

        // Assert
        model1.Should().NotBe(model2);
        (model1 == model2).Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var modelString = "model-tostring-test";
        var model = CodexModel.Parse(modelString);

        // Act
        var result = model.ToString();

        // Assert
        result.Should().Be(modelString);
    }

    [Fact]
    public void PredefinedModels_AreDistinct()
    {
        // Act
        var gpt51CodexMax = CodexModel.Gpt51CodexMax;
        var gpt51 = CodexModel.Gpt51Codex;
        var gpt51Mini = CodexModel.Gpt51CodexMini;
        var gpt51General = CodexModel.Gpt51;
        var defaultModel = CodexModel.Default;

        // Assert
        gpt51CodexMax.Should().Be(defaultModel);
        gpt51CodexMax.Should().NotBe(gpt51);
        gpt51.Should().NotBe(gpt51Mini);
        gpt51Mini.Should().NotBe(gpt51General);
        gpt51General.Should().NotBe(gpt51CodexMax);
    }
}
