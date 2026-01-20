// ============================================================================
// Token Cache Helper
// ============================================================================
// Provides persistent token caching to avoid repeated login prompts.
// Tokens are stored in the user's local application data folder:
//   %LOCALAPPDATA%\McpEnterpriseClient\msal_token_cache.bin
//
// Cache Behavior:
//   - Before token access: Load cached tokens from disk
//   - After token access: Save updated tokens if changed
//   - Tokens include access tokens, refresh tokens, and account info
// ============================================================================

using Microsoft.Identity.Client;

namespace McpEnterpriseClient.Authentication;

/// <summary>
/// Provides persistent token caching to a local file.
/// Avoids repeated authentication prompts by storing tokens between sessions.
/// </summary>
/// <remarks>
/// <para>
/// Token cache location: <c>%LOCALAPPDATA%\McpEnterpriseClient\msal_token_cache.bin</c>
/// </para>
/// <para>
/// To clear cached credentials, delete the cache file or call:
/// <code>Remove-Item "$env:LOCALAPPDATA\McpEnterpriseClient\msal_token_cache.bin"</code>
/// </para>
/// </remarks>
public class TokenCacheHelper
{
    private static readonly string TokenCacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpEnterpriseClient", "msal_token_cache.bin");

    /// <summary>
    /// Enables persistent token caching to a file.
    /// Tokens are cached locally so users don't need to re-authenticate every time.
    /// </summary>
    /// <param name="tokenCache">The MSAL token cache to configure.</param>
    /// <remarks>
    /// Registers before/after access callbacks that serialize tokens to disk.
    /// Creates the cache directory if it doesn't exist.
    /// </remarks>
    public void EnableTokenCache(ITokenCache tokenCache)
    {
        var cacheDir = Path.GetDirectoryName(TokenCacheFile);
        if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        tokenCache.SetBeforeAccess(args =>
        {
            if (File.Exists(TokenCacheFile))
            {
                var data = File.ReadAllBytes(TokenCacheFile);
                args.TokenCache.DeserializeMsalV3(data);
            }
        });

        tokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
            {
                var data = args.TokenCache.SerializeMsalV3();
                File.WriteAllBytes(TokenCacheFile, data);
            }
        });
    }
}
