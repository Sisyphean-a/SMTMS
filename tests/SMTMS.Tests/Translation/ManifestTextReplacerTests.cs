using FluentAssertions;
using SMTMS.Core.Helpers;

namespace SMTMS.Tests.Translation;

/// <summary>
/// ManifestTextReplacer 的单元测试
/// 测试正则表达式替换逻辑的各种边缘情况
/// </summary>
public class ManifestTextReplacerTests
{
    #region 中文检测测试

    [Fact]
    public void ContainsChinese_WithChineseText_ReturnsTrue()
    {
        // Arrange
        var text = "这是中文";

        // Act
        var result = ManifestTextReplacer.ContainsChinese(text);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsChinese_WithEnglishText_ReturnsFalse()
    {
        // Arrange
        var text = "This is English";

        // Act
        var result = ManifestTextReplacer.ContainsChinese(text);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsChinese_WithMixedText_ReturnsTrue()
    {
        // Arrange
        var text = "Mod名称";

        // Act
        var result = ManifestTextReplacer.ContainsChinese(text);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ContainsChinese_WithNullOrEmpty_ReturnsFalse(string? text)
    {
        // Act
        var result = ManifestTextReplacer.ContainsChinese(text!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Name 字段替换测试

    [Fact]
    public void ReplaceName_WithValidJson_ReplacesSuccessfully()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Original Name",
                              "Author": "Test Author"
                            }
                            """;
        const string newName = "新名称";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        result.Should().Contain("""
                                "Name": "新名称"
                                """);
        result.Should().Contain("""
                                "Author": "Test Author"
                                """); // 其他字段不变
    }

    [Fact]
    public void ReplaceName_WithComments_PreservesComments()
    {
        // Arrange - SMAPI manifest.json 通常包含注释
        const string json = """
                            {
                              // This is a comment
                              "Name": "Original Name",
                              "Author": "Test Author" // Inline comment
                            }
                            """;
        const string newName = "新名称";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        result.Should().Contain("// This is a comment");
        result.Should().Contain("// Inline comment");
        result.Should().Contain("""
                                "Name": "新名称"
                                """);
    }

    [Fact]
    public void ReplaceName_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        const string json = """{"Name": "Original"}""";
        const string newName = "名称\"带引号\"";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        // JsonConvert.ToString 会自动转义引号,所以结果应该包含转义后的引号
        result.Should().Contain("名称");
        result.Should().Contain("带引号");
    }

    [Fact]
    public void ReplaceName_WithoutNameField_ReturnsOriginal()
    {
        // Arrange
        const string json = """{"Author": "Test"}""";
        const string newName = "新名称";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        result.Should().Be(json); // 未修改
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ReplaceName_WithNullOrEmptyName_ReturnsOriginal(string? newName)
    {
        // Arrange
        const string json = """{"Name": "Original"}""";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName!);

        // Assert
        result.Should().Be(json);
    }

    #endregion

    #region Description 字段替换测试

    [Fact]
    public void ReplaceDescription_WithValidJson_ReplacesSuccessfully()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Test Mod",
                              "Description": "Original Description"
                            }
                            """;
        const string newDesc = "新描述";

        // Act
        var result = ManifestTextReplacer.ReplaceDescription(json, newDesc);

        // Assert
        result.Should().Contain("""
                                "Description": "新描述"
                                """);
        result.Should().Contain("""
                                "Name": "Test Mod"
                                """); // 其他字段不变
    }

    [Fact]
    public void ReplaceDescription_WithMultilineDescription_ReplacesCorrectly()
    {
        // Arrange
        const string json = """
                            {
                              "Description": "This is a long description
                            that spans multiple lines"
                            }
                            """;
        const string newDesc = "简短描述";

        // Act
        var result = ManifestTextReplacer.ReplaceDescription(json, newDesc);

        // Assert
        result.Should().Contain("""
                                "Description": "简短描述"
                                """);
    }

    [Fact]
    public void ReplaceDescription_WithoutDescriptionField_ReturnsOriginal()
    {
        // Arrange
        const string json = """{"Name": "Test"}""";
        const string newDesc = "新描述";

        // Act
        var result = ManifestTextReplacer.ReplaceDescription(json, newDesc);

        // Assert
        result.Should().Be(json);
    }

    #endregion

    #region 同时替换 Name 和 Description 测试

    [Fact]
    public void ReplaceNameAndDescription_WithBothFields_ReplacesBoth()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Original Name",
                              "Description": "Original Description"
                            }
                            """;
        const string newName = "新名称";
        var newDesc = "新描述";

        // Act
        var result = ManifestTextReplacer.ReplaceNameAndDescription(json, newName, newDesc);

        // Assert
        result.Should().Contain("""
                                "Name": "新名称"
                                """);
        result.Should().Contain("""
                                "Description": "新描述"
                                """);
    }

    [Fact]
    public void ReplaceNameAndDescription_WithOnlyName_ReplacesOnlyName()
    {
        // Arrange
        const string json = """
                   {
                     "Name": "Original Name",
                     "Description": "Original Description"
                   }
                   """;
        const string newName = "新名称";

        // Act
        var result = ManifestTextReplacer.ReplaceNameAndDescription(json, newName, null);

        // Assert
        result.Should().Contain("""
                                "Name": "新名称"
                                """);
        result.Should().Contain("""
                                "Description": "Original Description"
                                """);
    }

    #endregion

    #region 字段存在性检测测试

    [Fact]
    public void HasNameField_WithNameField_ReturnsTrue()
    {
        // Arrange
        const string json = """{"Name": "Test"}""";

        // Act
        var result = ManifestTextReplacer.HasNameField(json);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasNameField_WithoutNameField_ReturnsFalse()
    {
        // Arrange
        const string json = """{"Author": "Test"}""";

        // Act
        var result = ManifestTextReplacer.HasNameField(json);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasDescriptionField_WithDescriptionField_ReturnsTrue()
    {
        // Arrange
        const string json = """{"Description": "Test"}""";

        // Act
        var result = ManifestTextReplacer.HasDescriptionField(json);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region 边缘情况测试

    [Fact]
    public void ReplaceName_WithEmptyJson_ReturnsOriginal()
    {
        // Arrange
        const string json = "";
        const string newName = "新名称";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        result.Should().Be(json);
    }

    [Fact]
    public void ReplaceName_WithWhitespaceInFieldName_StillMatches()
    {
        // Arrange - JSON 可能有不同的空白格式
        const string json = """{"Name"  :  "Original"}""";
        const string newName = "新名称";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        result.Should().Contain("""
                                "新名称"
                                """);
    }

    [Fact]
    public void ReplaceName_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        const string json = """{"Name": "Original"}""";
        const string newName = "模组名称 🎮";

        // Act
        var result = ManifestTextReplacer.ReplaceName(json, newName);

        // Assert
        result.Should().Contain("模组名称 🎮");
    }

    #endregion
}


