using Microsoft.Identity.Client;

namespace McpEnterpriseClient.Authentication;

/// <summary>
/// Handles persistent token caching to a local file.
/// </summary>
public class TokenCacheHelper
{
    private static readonly string TokenCacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpEnterpriseClient", "msal_token_cache.bin");

    /// <summary>
    /// Enables persistent token caching to a file.
    /// Tokens are cached locally so users don't need to re-authenticate every time.
    /// </summary>
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
