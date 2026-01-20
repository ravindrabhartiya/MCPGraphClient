// ============================================================================
// Environment Variables Test Collection
// ============================================================================
// Defines a test collection for tests that modify environment variables.
// xUnit runs tests in the same collection sequentially, preventing race
// conditions when multiple tests modify the same environment variables.
//
// Usage:
//   [Collection("Environment Variables")]
//   public class MyTests { ... }
// ============================================================================

namespace McpEnterpriseClient.Tests;

/// <summary>
/// Test collection for tests that modify environment variables.
/// </summary>
/// <remarks>
/// <para>
/// xUnit runs tests in parallel by default. Tests that modify shared state
/// (like environment variables) can interfere with each other.
/// </para>
/// <para>
/// By placing tests in the same collection, xUnit runs them sequentially,
/// preventing race conditions.
/// </para>
/// <para>
/// Apply to test classes with: <c>[Collection("Environment Variables")]</c>
/// </para>
/// </remarks>
[CollectionDefinition("Environment Variables")]
public class EnvVarTestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
