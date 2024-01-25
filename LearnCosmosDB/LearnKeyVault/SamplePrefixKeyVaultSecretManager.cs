using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace LearnKeyVault;

public class SamplePrefixKeyVaultSecretManager : KeyVaultSecretManager
{
    private readonly string _prefix;

    public SamplePrefixKeyVaultSecretManager(string prefix)
        => _prefix = $"{prefix}-";

    public override bool Load(SecretProperties properties)
        => properties.Name.StartsWith(_prefix);

    public override string GetKey(KeyVaultSecret secret)
        => secret.Name[_prefix.Length..].Replace("--", ConfigurationPath.KeyDelimiter);
}
