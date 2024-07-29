// See https://aka.ms/new-console-template for more information

using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LearnKeyVault;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/***
 * NOTE: to work with keyvault, we need:
 * Use azure cli to create the keyvault, in this way, we can get policy enabled.
 * We need to add policy for the enterprise application to make it accessible.
 * If using portal to create the keyvault, from my testing, it would not works and got following error:
 *    Caller is not authorized to perform action on resource.
 * **/

var env = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT") ?? "Development";

var configurationBuilder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{env}.json", true, true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var configuration = configurationBuilder.Build();

var keyVaultUri = new Uri(configuration["KeyVaultUri"]!.ToString());

TokenCredential credental = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
{
    AuthorityHost = keyVaultUri.Host.EndsWith(".cn", StringComparison.OrdinalIgnoreCase) ? AzureAuthorityHosts.AzureChina : AzureAuthorityHosts.AzurePublicCloud
});
//if (builder.Environment.IsProduction())
{
    //please refer to https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-8.0
    configurationBuilder.AddAzureKeyVault(
        keyVaultUri, credental, new SamplePrefixKeyVaultSecretManager("LearnKeyVault"));
    configuration = configurationBuilder.Build();
}

Console.WriteLine("Environment: {0}", env);
Console.WriteLine("KeyVault: {0}", keyVaultUri.AbsoluteUri);
Console.WriteLine("Key: KeySample, Value: {0}", configuration["KeySample"]);

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration)
    .AddLogging(loggingBuilder =>
    {
        loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole();
    });

services.AddAzureClients(builder =>
{
    builder.AddSecretClient(keyVaultUri)
    .WithCredential(credental);
});


var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var userDetail = new { name = "Shawn", country = "China" };
logger.LogInformation("[START] {Name}, Data: {UserDetail}", nameof(Program), userDetail);


Console.WriteLine();
Console.WriteLine("[Demo] logging level configuration:");
logger.LogInformation("TypeName: {class}", typeof(Program).FullName);
logger.LogInformation("this is information message");
logger.LogWarning("this is warnning message");
logger.LogError("this is error message");

logger.Log(LogLevel.Information, new EventId(1, "Hello"), userDetail, null, (o, e) => "[TEST] test complex data");


Console.WriteLine();
Console.WriteLine("[Demo] KeyVault API calls:");

var keyVaultClient = serviceProvider.GetRequiredService<SecretClient>();

try
{
    var kvOnline = await keyVaultClient.GetSecretAsync(Constants.KeyOnline01);

    logger.LogInformation("Found {key} from Secret: '{value}'", Constants.KeyOnline01, kvOnline?.Value.Value);
}
catch (Azure.RequestFailedException rfe)
{
    if (rfe.Status == 404)
    {
        var value = $"{DateTime.Now:o}";
        logger.LogInformation("{key} was not found and newly trying to set with '{value}'", Constants.KeyOnline01, value);

        await keyVaultClient.SetSecretAsync(Constants.KeyOnline01, value);
    }
}
catch (Exception)
{
    throw;
}

await Task.Delay(100);

Console.WriteLine();
Console.WriteLine("[Demo] configuration built with KeyVault:");

logger.LogInformation("If you have KeyVault secret set with key 'Key01', it will overwrite existing one in appsettings.");

logger.LogInformation("Configuration value for KeyOneline01: '{value}'", configuration[Constants.KeyOnline01]);
logger.LogInformation("Configuration value for Key01: '{value}'", configuration[Constants.Key01]);
logger.LogInformation("Configuration value for Key02: '{value}'", configuration[Constants.Key02]);

await Task.Delay(300);
Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();











