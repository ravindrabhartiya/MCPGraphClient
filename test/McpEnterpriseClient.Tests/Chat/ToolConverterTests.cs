using McpEnterpriseClient.Chat;

namespace McpEnterpriseClient.Tests.Chat;

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
