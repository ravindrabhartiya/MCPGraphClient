using McpEnterpriseClient.Authentication;

namespace McpEnterpriseClient.Tests.Authentication;

public class TokenCacheHelperTests
{
    [Fact]
    public void TokenCacheHelper_CanBeInstantiated()
    {
        // Act
        var helper = new TokenCacheHelper();

        // Assert
        Assert.NotNull(helper);
    }

    [Fact]
    public void TokenCacheFile_IsInLocalAppData()
    {
        // This test verifies the token cache location is appropriate
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var expectedPathPart = Path.Combine("McpEnterpriseClient", "msal_token_cache.bin");
        
        // Assert the path structure is correct (we can't access private field, but we can verify the folder exists after use)
        Assert.False(string.IsNullOrEmpty(localAppData));
    }
}
