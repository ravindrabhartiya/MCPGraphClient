namespace McpEnterpriseClient.Tests;

/// <summary>
/// Collection definition for tests that modify environment variables.
/// Tests in the same collection run sequentially, not in parallel.
/// </summary>
[CollectionDefinition("Environment Variables")]
public class EnvVarTestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
