using System.Security.Cryptography.X509Certificates;

namespace McpEnterpriseClient.Utilities;

/// <summary>
/// Helper class for loading certificates from the Windows Certificate Store.
/// </summary>
public static class CertificateHelper
{
    /// <summary>
    /// Loads a certificate from the Windows Certificate Store by thumbprint.
    /// Searches both CurrentUser and LocalMachine stores.
    /// </summary>
    public static X509Certificate2 LoadFromStore(string thumbprint)
    {
        // Normalize thumbprint (remove spaces, convert to uppercase)
        thumbprint = thumbprint.Replace(" ", "").ToUpperInvariant();

        // Try CurrentUser store first
        var cert = FindInStore(thumbprint, StoreLocation.CurrentUser);
        if (cert != null) return cert;

        // Try LocalMachine store
        cert = FindInStore(thumbprint, StoreLocation.LocalMachine);
        if (cert != null) return cert;

        throw new InvalidOperationException(
            $"Certificate with thumbprint '{thumbprint}' not found in CurrentUser or LocalMachine certificate stores.\n" +
            "To install a certificate:\n" +
            "  1. Double-click the .pfx file\n" +
            "  2. Select 'Current User' or 'Local Machine'\n" +
            "  3. Follow the wizard to import");
    }

    private static X509Certificate2? FindInStore(string thumbprint, StoreLocation location)
    {
        using var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly);

        var certificates = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);

        return certificates.Count > 0 ? certificates[0] : null;
    }
}
