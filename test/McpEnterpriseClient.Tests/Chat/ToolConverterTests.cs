// ============================================================================
// ToolConverter Unit Tests
// ============================================================================
// Tests for the ToolConverter class that transforms MCP tools to OpenAI format.
// ============================================================================

using McpEnterpriseClient.Chat;

namespace McpEnterpriseClient.Tests.Chat;

/// <summary>
/// Unit tests for <see cref="ToolConverter"/> class.
/// </summary>
/// <remarks>
/// Tests verify that MCP tools are correctly converted to OpenAI ChatTool format
/// with appropriate parameter schemas.
/// </remarks>
public class ToolConverterTests
{
    private readonly ToolConverter _converter;

    public ToolConverterTests()
    {
        _converter = new ToolConverter();
    }

    [Fact]
    public void ToolConverter_CanBeInstantiated()
    {
        // Act
        var converter = new ToolConverter();

        // Assert
        Assert.NotNull(converter);
    }

    [Fact]
    public void ConvertMcpToolsToChatTools_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var mcpTools = new List<ModelContextProtocol.Client.McpClientTool>();

        // Act
        var result = _converter.ConvertMcpToolsToChatTools(mcpTools);

        // Assert
        Assert.Empty(result);
    }
}
