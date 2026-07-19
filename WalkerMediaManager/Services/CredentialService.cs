using Windows.Security.Credentials;

namespace WalkerMediaManager.Services;

public sealed class CredentialService
{
    private const string ResourceName = "WalkerMediaManager.Plex";
    private const string UserName = "PlexToken";

    public string LoadPlexToken()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(ResourceName, UserName);
            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void SavePlexToken(string token)
    {
        var vault = new PasswordVault();
        try
        {
            var existing = vault.Retrieve(ResourceName, UserName);
            vault.Remove(existing);
        }
        catch
        {
            // No existing credential.
        }

        if (!string.IsNullOrWhiteSpace(token))
            vault.Add(new PasswordCredential(ResourceName, UserName, token.Trim()));
    }
}
