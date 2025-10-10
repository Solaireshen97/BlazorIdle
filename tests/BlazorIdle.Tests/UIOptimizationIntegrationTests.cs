using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Step 6: 整体集成与优化 - UI集成测试
/// 验证前端UI优化的关键功能点
/// </summary>
public class UIOptimizationIntegrationTests
{
    [Fact]
    public void Step6_LoadingIndicator_CSSClassExists()
    {
        // Arrange & Act
        var cssPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "wwwroot", "css", "app-extra.css"
        );

        // Assert
        Assert.True(File.Exists(cssPath), "app-extra.css 文件应该存在");
        
        var cssContent = File.ReadAllText(cssPath);
        Assert.Contains(".loading-indicator", cssContent);
        Assert.Contains(".loading-indicator-inline", cssContent);
        Assert.Contains(".character-card", cssContent);
    }

    [Fact]
    public void Step6_TabNavigation_CSSExists()
    {
        // Arrange & Act
        var cssPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "wwwroot", "css", "app-extra.css"
        );

        var cssContent = File.ReadAllText(cssPath);

        // Assert
        Assert.Contains(".nav-tabs", cssContent);
        Assert.Contains(".tab-content", cssContent);
        Assert.Contains(".tab-pane", cssContent);
    }

    [Fact]
    public void Step6_CharactersRazor_HasKeyAttributes()
    {
        // Arrange & Act
        var razorPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "Pages", "Characters.razor"
        );

        // Assert
        Assert.True(File.Exists(razorPath), "Characters.razor 文件应该存在");
        
        var razorContent = File.ReadAllText(razorPath);
        
        // 验证 @key 属性的使用（性能优化）
        Assert.Contains("@key=\"@character.Id\"", razorContent);
        Assert.Contains("@key=\"@i\"", razorContent);
    }

    [Fact]
    public void Step6_LoadingIndicator_ExistsInCharactersRazor()
    {
        // Arrange & Act
        var razorPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "Pages", "Characters.razor"
        );

        var razorContent = File.ReadAllText(razorPath);

        // Assert
        Assert.Contains("loading-indicator", razorContent);
        Assert.Contains("Step 6", razorContent); // 验证包含 Step 6 的注释标记
    }

    [Fact]
    public void Step6_ResponsiveDesign_MediaQueryExists()
    {
        // Arrange & Act
        var cssPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "wwwroot", "css", "app-extra.css"
        );

        var cssContent = File.ReadAllText(cssPath);

        // Assert
        Assert.Contains("@media (max-width: 768px)", cssContent);
        Assert.Contains(".character-list", cssContent);
    }

    [Fact]
    public void Step6_CSSAnimations_Exists()
    {
        // Arrange & Act
        var cssPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "wwwroot", "css", "app-extra.css"
        );

        var cssContent = File.ReadAllText(cssPath);

        // Assert
        Assert.Contains("@keyframes fadeIn", cssContent);
        Assert.Contains("@keyframes tabFadeIn", cssContent);
        Assert.Contains("animation:", cssContent);
    }

    [Fact]
    public void Step6_UIComponents_EnhancedStyling()
    {
        // Arrange & Act
        var cssPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "BlazorIdle", "wwwroot", "css", "app-extra.css"
        );

        var cssContent = File.ReadAllText(cssPath);

        // Assert - 验证增强的UI组件样式
        Assert.Contains(".character-card:hover", cssContent);
        Assert.Contains(".character-card.selected", cssContent);
        Assert.Contains(".panel:hover", cssContent);
        Assert.Contains("transition:", cssContent);
    }
}
