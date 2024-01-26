using Azure.Identity;
using LearnCosmosDb;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using var channel = new InMemoryChannel();

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", true)
    .AddUserSecrets<Program>()
    .Build();

ServiceCollection services = new();

var connectionString = configuration.GetConnectionString("COSMOSDB_DEFAULT");

if (string.IsNullOrEmpty(connectionString) || connectionString == "<TO_BE_SET>")
{
    Console.WriteLine("Please set the connection string for CosmosDB");
    return;
}

services.Configure<TelemetryConfiguration>(config => config.TelemetryChannel = channel);
services.AddLogging(builder =>
 {
     var appInsightsConnectionString = configuration.GetConnectionString("APPLICATIONINSIGHTS_CONNECTION_STRING");
     if (!string.IsNullOrEmpty(appInsightsConnectionString))
     {
         services.Configure<TelemetryConfiguration>(config =>
         {
             var credential = new DefaultAzureCredential();
             config.SetAzureTokenCredential(credential);
         });
         builder.AddApplicationInsights(
             configureTelemetryConfiguration: (config) => config.ConnectionString = configuration.GetConnectionString("APPLICATIONINSIGHTS_CONNECTION_STRING"),
             configureApplicationInsightsLoggerOptions: (options) => { }
         );
         Console.WriteLine("AddApplicationInsights got applied.");
     }
     builder.AddConsole();
 });

services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddClient<CosmosClient, CosmosClientOptions>((options) =>
    {
        options.HttpClientFactory = () => new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        options.SerializerOptions = new()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        };
        return new CosmosClient(connectionString, options);
    });
});

IServiceProvider serviceProvider = services.BuildServiceProvider();
ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("starting cosmos db demo, with optional application insights");

using (logger.BeginScope(new Dictionary<string, object> { ["MyKey"] = "MyValue", ["RequestId"] = Guid.NewGuid().ToString(), ["User"] = new { Name = "Shawn", Id = "123" } }))
{
    logger.LogError("An example of an Error level message");
    logger.LogInformation("An example of an Information level message");
    logger.LogInformation("Data: {Data}", new { title = "developer", company = "happy company", address = new { city = "SH", country = "China" } });
    logger.Log(LogLevel.Information, new EventId(1, "TestLog"),
        new { field1 = "good", field2 = "bad", field3 = "ugly", address = new { city = "FJ", country = "China" } },
        null, (o, e) => "An example of TState with complex object");
}


try
{
    //CosmosSerializationOptions serializerOptions = new()
    //{
    //    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    //};

    //CosmosClientOptions options = new()
    //{
    //    HttpClientFactory = () => new HttpClient(new HttpClientHandler()
    //    {
    //        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    //    }),
    //    ConnectionMode = ConnectionMode.Gateway
    //};
    ////using CosmosClient client = new(connectionString, options); 
    //using CosmosClient client = new CosmosClientBuilder(connectionString).WithConnectionModeGateway()
    //    .WithSerializerOptions(serializerOptions)
    //    .WithHttpClientFactory(() => new HttpClient(new HttpClientHandler()
    //    {
    //        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    //    }))
    //    .Build();

    using CosmosClient client = serviceProvider.GetRequiredService<CosmosClient>();

    var account = await client.ReadAccountAsync();

    logger.LogInformation($"account.Id: {account.Id}; ReadableRegions: {string.Join(",", account.ReadableRegions.Select(x => x.Name))}; account.Consistency: {account.Consistency.DefaultConsistencyLevel}");

    Database database = await //client.CreateDatabaseAsync("CARE")
        client.CreateDatabaseIfNotExistsAsync("CARE");

    logger.LogInformation($"database.Id : {database.Id}");

    var containerProperties = new ContainerProperties
    {
        Id = configuration["CollectionName"] ?? "PhoneStatusInfo",
        PartitionKeyPath = "/phone",
        DefaultTimeToLive = -1
    };
    Container container = await database.CreateContainerIfNotExistsAsync(containerProperties, 400);
    //Container container = await database.CreateContainerAsync(configuration["CollectionName"] ?? "PhoneStatusInfo", "/phone", 400);
    logger.LogInformation($"container.Id : {container.Id}");

    var phone = $"1{DateTime.Now:yyddHHmmss}";

    await CreateDemoItemsWithTtlAsync(container, "6268889999");

    await CreateDemoItemsAsync(container, phone);

    var phone2Search = phone;

    var searchResult = await SearchItemsByPhoneAsync(container, phone2Search);

    if (searchResult.Item1.Count == 0)
        return;

    var pickOne = searchResult.Item1.Last();

    await ReadOneAsync(container, pickOne);
    await Task.Delay(1200);

    var upsertOne = pickOne;

    await UpsertOneAsync(container, upsertOne);
    await Task.Delay(1200);

    var patchOne = pickOne;

    await ReadOneAsync(container, pickOne);
    await Task.Delay(1000);

    await PatchOneAsync(container, patchOne.Id, patchOne.Phone,
        [
            PatchOperation.Set("/lineOfBiz", "test"),
            PatchOperation.Set("/status", PhoneStatus.Grey),
            PatchOperation.Set("/comment", $"Updated: {DateTime.Now}, with new status: {PhoneStatus.Grey}"),
            PatchOperation.Add("/history/0", new PhoneStatusRow() { CreateDate = DateTime.Now, Status = PhoneStatus.Grey }),
            PatchOperation.Add("/history/0", new PhoneStatusRow() { CreateDate = DateTime.Now.AddMinutes(-5), Status = PhoneStatus.Black }),
            PatchOperation.Add("/history/0", new PhoneStatusRow() { CreateDate = DateTime.Now.AddMinutes(-15), Status = PhoneStatus.Clear }), //processed lastly
        ]);
    await Task.Delay(1200);

    await PatchOneAsync(container, patchOne.Id, patchOne.Phone,
        [
            PatchOperation.Set("/status", PhoneStatus.Clear),
            PatchOperation.Add("/history/0", new PhoneStatusRow() { CreateDate = DateTime.Now, Status = PhoneStatus.Clear }),
        ]);
    await Task.Delay(200);

    {
        logger.LogInformation($"{Environment.NewLine}Press Enter to remove history[1], or any other key to continue.");
        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Enter)
        {
            await PatchOneAsync(container, patchOne.Id, patchOne.Phone,
                [
                    PatchOperation.Set("/comment", $"Updated: {DateTime.Now}, with history[1] removed"),
                    PatchOperation.Remove("/history/1"),
                ]);
            await Task.Delay(200);
        }
    }


    await DeleteItemsAsync(container, searchResult.Item1);

    logger.LogInformation($"{Environment.NewLine}[End] Exited");
}
catch (Exception ex)
{
    logger.LogError(ex, "Exception occurred.");
}
finally
{
    channel.Flush();
    await Task.Delay(1000);
}

async Task CreateDemoItemsWithTtlAsync(Container container, string phone, int ttl = 120)
{
    logger.LogInformation($"\r\n====Create Items with ttl {ttl} ====");

    foreach (var p in new List<string> { "Biz-X", "Biz-Y" }.Take(10).ToList())
    {
        var data = new PhoneStatusInfo()
        {
            Id = Guid.NewGuid().ToString(),
            Phone = phone,
            LineOfBiz = p,
            Comment = $"this item got created with TTL {ttl} seconds",
            Status = PhoneStatus.Black,
            History = new List<PhoneStatusRow>
            {
                new() { CreateDate = DateTime.Now, Status = PhoneStatus.Black}
            },
            Ttl = ttl
        };

        var result = await container.CreateItemAsync<PhoneStatusInfo>(data, requestOptions: new ItemRequestOptions { });

        logger.LogInformation($"Create [{data.Id},{phone}] with ttl {ttl}, result.StatusCode : {result.StatusCode}, result.RequestCharge : {result.RequestCharge}");
    }
}

async Task CreateDemoItemsAsync(Container container, string phone)
{
    logger.LogInformation("\r\n====Create Items without ttl====");

    foreach (var p in new List<string> { "Biz-A", "Biz-B", "Biz-C" }.Take(10).ToList())
    {
        var data = new PhoneStatusInfo()
        {
            Id = Guid.NewGuid().ToString(),
            Phone = phone,
            LineOfBiz = p,
            Status = PhoneStatus.Black,
            History = new List<PhoneStatusRow>
            {
                new() { CreateDate = DateTime.Now, Status = PhoneStatus.Black}
            }
        };

        var result = await container.CreateItemAsync<PhoneStatusInfo>(data);

        logger.LogInformation($"Create [{data.Id},{phone}] result.StatusCode : {result.StatusCode}, result.RequestCharge : {result.RequestCharge}");
    }
}

async Task<Tuple<List<PhoneStatusInfo>, double>> SearchItemsByPhoneAsync(Container container, string phone2Search)
{
    logger.LogInformation("\r\n====Search by Phone ====");

    var queryable = container.GetItemLinqQueryable<PhoneStatusInfo>();
    var matches = queryable.Where(p => p.Phone == phone2Search).OrderBy(p => p.CreateDate);//.Skip(1);
    using var linqFeed = matches.ToFeedIterator();

    var result = new List<PhoneStatusInfo>();

    double totalRequestChange = 0;
    while (linqFeed.HasMoreResults)
    {
        var resp = await linqFeed.ReadNextAsync();

        foreach (var item in resp)
        {
            logger.LogInformation($"[Start query]:\t{item.Id}, {item.Phone}, {item.LineOfBiz}, {item.CreateDate}, charged: [{resp.RequestCharge}]");
            result.Add(item);

            totalRequestChange += resp.RequestCharge;
        }
    }
    logger.LogInformation($"totalRequestChange RUs for search phone: {totalRequestChange}");

    return Tuple.Create(result, totalRequestChange);
}

async Task ReadOneAsync(Container container, PhoneStatusInfo pickOne)
{
    logger.LogInformation($"\r\n====Read Item for id: {pickOne.Id} and phone '{pickOne.Phone}'====");

    try
    {
        var readResult = await container.ReadItemAsync<PhoneStatusInfo>(pickOne.Id, new PartitionKey(pickOne.Phone));

        logger.LogInformation($"readResult.RequestCharge : {readResult.RequestCharge}");
        logger.LogInformation($"""
            readResult.Resource :
              Id: {readResult.Resource.Id}
              phone:{readResult.Resource.Phone}
              Comment:{readResult.Resource.Comment}
              History count:{readResult.Resource.History.Count}
            """);
    }
    catch (CosmosException ex)
    {
        logger.LogInformation($"ReadItem got CosmosException occurred: StatusCode is {ex.StatusCode}, RequestCharge: {ex.RequestCharge}");
    }
    catch (Exception ex)
    {
        logger.LogInformation($"ReadItem got error occurred[{ex.GetType().FullName}]: {ex.Message}");
    }
}

async Task UpsertOneAsync(Container container, PhoneStatusInfo upsertOne)
{
    logger.LogInformation("\r\n====Upsert====");

    try
    {
        var upsertData_1 = new PhoneStatusInfo()
        {
            Id = upsertOne.Id!,
            Phone = upsertOne.Phone,
            Status = PhoneStatus.Clear,
            Comment = $"Updated: {DateTime.Now}",
            History = new List<PhoneStatusRow>(),
            CreateDate = DateTime.Parse("2023-12-28T10:01:53.8085827+08:00")
        };

        var upsertResult = await container.UpsertItemAsync<PhoneStatusInfo>(upsertData_1);

        logger.LogInformation($"upsertResult.RequestCharge : {upsertResult.RequestCharge}");
        logger.LogInformation($"""
            upsertResult.Resource :
              Id: {upsertResult.Resource.Id}
              phone:{upsertResult.Resource.Phone}
              Comment:{upsertResult.Resource.Comment}
              History count:{upsertResult.Resource.History.Count}
            """);
    }
    catch (CosmosException ex)
    {
        logger.LogInformation($"UpsertItemAsync got CosmosException occurred: StatusCode is {ex.StatusCode}, RequestCharge: {ex.RequestCharge}");
    }
    catch (Exception ex)
    {
        logger.LogInformation($"UpsertItemAsync got error occurred[{ex.GetType().FullName}]: {ex.Message}");
    }
}

async Task PatchOneAsync(Container container, string id2Patch, string phone2Patch, PatchOperation[] patchOperations)
{
    logger.LogInformation("\r\n====Patching====");

    var operationDetails = string.Join(";\r\n\t", patchOperations!.Select(item => $"# {item.OperationType} {item.Path}"));
    logger.LogInformation(message: "Operations:\r\n\t{OperationDetails}", operationDetails);

    var patchResult = await container.PatchItemAsync<PhoneStatusInfo>(id2Patch, new PartitionKey(phone2Patch), patchOperations);

    logger.LogInformation($"patchResult.RequestCharge : {patchResult.RequestCharge}");
    logger.LogInformation($"""
              patchResult.Resource :
              Id: {patchResult.Resource.Id}
              phone:{patchResult.Resource.Phone}
              Comment:{patchResult.Resource.Comment}
              History count:{patchResult.Resource.History.Count}
            """);
}

async Task DeleteItemsAsync(Container container, List<PhoneStatusInfo> item1)
{
    logger.LogInformation($"{Environment.NewLine}Press Enter to delete {item1.Count} items just newly created in this session, or any other key to exit.");
    var key = Console.ReadKey(true);

    if (key.Key != ConsoleKey.Enter) return;
    logger.LogInformation("\r\n====Deleting items====");

    double totalRequestCharges = 0;
    foreach (var item in item1)
    {
        var delResp = await container.DeleteItemAsync<PhoneStatusInfo>(item.Id, new PartitionKey(item.Phone));
        logger.LogInformation($"Delete {item.Id}/{item.Phone} took {delResp.RequestCharge} RUs");

        totalRequestCharges += delResp.RequestCharge;
    }

    logger.LogInformation($"Delete {item1.Count} documents took {totalRequestCharges} RUs in total");
}