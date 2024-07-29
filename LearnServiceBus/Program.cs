using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

/***
 * NOTE: to work with serviceBus, we need:
 * Use azure cli to create the ServiceBus resource, in this way, we can get policy enabled.
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

var serviceBusUri = new Uri(configuration["ServiceBusUri"]!.ToString());
var queueName = configuration["QueueName"]!.ToString();

TokenCredential credental = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
{
    AuthorityHost = serviceBusUri.Host.EndsWith(".cn", StringComparison.OrdinalIgnoreCase) ? AzureAuthorityHosts.AzureChina : AzureAuthorityHosts.AzurePublicCloud
});


Console.WriteLine("Environment: {0}", env);
Console.WriteLine("ServiceBus Uri: {0}", serviceBusUri);
Console.WriteLine("ServiceBus Queue Name: {0}", queueName);

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration)
    .AddLogging(loggingBuilder =>
    {
        loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole();
    });

services.AddAzureClients(clientBuilder =>
{
    //var serviceBusNs = configuration["ServiceBusConnection:fullyQualifiedNamespace"];
    clientBuilder.AddServiceBusClientWithNamespace(serviceBusUri.AbsoluteUri);
    clientBuilder.UseCredential(credental);
});


var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

Console.WriteLine();
Console.WriteLine("[Demo] ServiceBus:");

var serviceBusClient = serviceProvider.GetRequiredService<ServiceBusClient>();

Stopwatch stopwatch = Stopwatch.StartNew();
try
{
    int count = 5500;
    await Parallel.ForEachAsync(Enumerable.Range(1, count), async (x, y) =>
    {
        var sender = serviceBusClient.CreateSender(queueName);
        await sender.ScheduleMessageAsync(new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(new { User = "Shawn Lin", Age = 36, Id = x })),
            DateTime.UtcNow.AddSeconds(10));
        await sender.CloseAsync();
    });

    Console.WriteLine("Send {0} messages", count);
}
catch (Azure.RequestFailedException rfe)
{
    throw;
}
catch (Exception)
{
    throw;
}
stopwatch.Stop();
Console.WriteLine("took {0}ms.", stopwatch.ElapsedMilliseconds);

await Task.Delay(300);
Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();











