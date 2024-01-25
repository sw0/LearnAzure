// See https://aka.ms/new-console-template for more information

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LearnKeyVault;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


var env = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT") ?? "Development";

var configurationBuilder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{env}.json", true, true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var configuration = configurationBuilder.Build();
//if (builder.Environment.IsProduction())
{
    //please refer to https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-8.0
    configurationBuilder.AddAzureKeyVault(
        new Uri($"https://{configuration["KeyVaultName"]}.vault.azure.net/"),
        new DefaultAzureCredential(), 
        new SamplePrefixKeyVaultSecretManager("LearnKeyVault"));
    configuration = configurationBuilder.Build();
}

Console.WriteLine("Environment: {0}", env);
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
    builder.AddSecretClient(new Uri($"https://{configuration["KeyVaultName"]}.vault.azure.net/"))
    .WithCredential(new DefaultAzureCredential());
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

var found = await keyVaultClient.GetSecretAsync(LearnKeyVault.Constants.DefaultCosmosDBConnectionString);

logger.LogInformation("Found DefaultCosmosDBConnectionString from Secret: '{value}'", found?.Value.Value);


Console.WriteLine();
Console.WriteLine("[Demo] configuration built with KeyVault:");

logger.LogInformation("If you have KeyVault secret set with key 'Key01', it will overwrite existing one in appsettings.");

logger.LogInformation("Found value for Key02: '{value}'", configuration[LearnKeyVault.Constants.Key02]);

await Task.Delay(300);
Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();











