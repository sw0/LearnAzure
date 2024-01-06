using LearnCosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


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

var serviceProvider = services.BuildServiceProvider();

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

Console.WriteLine($"account.Id: {account.Id}; ReadableRegions: {string.Join(",", account.ReadableRegions.Select(x => x.Name))}; account.Consistency: {account.Consistency.DefaultConsistencyLevel}");

Database database = await //client.CreateDatabaseAsync("CARE")
    client.CreateDatabaseIfNotExistsAsync("CARE")
;

Console.WriteLine($"database.Id : {database.Id}");

var containerProperties = new ContainerProperties
{
    Id = configuration["CollectionName"] ?? "PhoneStatusInfo",
    PartitionKeyPath = "/phone",
    DefaultTimeToLive = -1
};
Container container = await database.CreateContainerIfNotExistsAsync(containerProperties, 400);
//Container container = await database.CreateContainerAsync(configuration["CollectionName"] ?? "PhoneStatusInfo", "/phone", 400);
Console.WriteLine($"container.Id : {container.Id}");

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
    Console.WriteLine($"{Environment.NewLine}Press Enter to remove history[1], or any other key to continue.");
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
Console.WriteLine($"{Environment.NewLine}[End] Exited");

static async Task CreateDemoItemsWithTtlAsync(Container container, string phone, int ttl = 120)
{
    Console.WriteLine($"\r\n====Create Items with ttl {ttl} ====");

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

        Console.WriteLine($"Create [{data.Id},{phone}] with ttl {ttl}, result.StatusCode : {result.StatusCode}, result.RequestCharge : {result.RequestCharge}");
    }
}

static async Task CreateDemoItemsAsync(Container container, string phone)
{
    Console.WriteLine("\r\n====Create Items without ttl====");

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

        Console.WriteLine($"Create [{data.Id},{phone}] result.StatusCode : {result.StatusCode}, result.RequestCharge : {result.RequestCharge}");
    }
}

static async Task<Tuple<List<PhoneStatusInfo>, double>> SearchItemsByPhoneAsync(Container container, string phone2Search)
{
    Console.WriteLine("\r\n====Search by Phone ====");

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
            Console.WriteLine($"[Start query]:\t{item.Id}, {item.Phone}, {item.LineOfBiz}, {item.CreateDate}, charged: [{resp.RequestCharge}]");
            result.Add(item);

            totalRequestChange += resp.RequestCharge;
        }
    }
    Console.WriteLine($"totalRequestChange RUs for search phone: {totalRequestChange}");

    return Tuple.Create(result, totalRequestChange);
}

static async Task ReadOneAsync(Container container, PhoneStatusInfo pickOne)
{
    Console.WriteLine($"\r\n====Read Item for id: {pickOne.Id} and phone '{pickOne.Phone}'====");

    try
    {
        var readResult = await container.ReadItemAsync<PhoneStatusInfo>(pickOne.Id, new PartitionKey(pickOne.Phone));

        Console.WriteLine($"readResult.RequestCharge : {readResult.RequestCharge}");
        Console.WriteLine($"""
            readResult.Resource :
              Id: {readResult.Resource.Id}
              phone:{readResult.Resource.Phone}
              Comment:{readResult.Resource.Comment}
              History count:{readResult.Resource.History.Count}
            """);
    }
    catch (CosmosException ex)
    {
        Console.WriteLine($"ReadItem got CosmosException occurred: StatusCode is {ex.StatusCode}, RequestCharge: {ex.RequestCharge}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ReadItem got error occurred[{ex.GetType().FullName}]: {ex.Message}");
    }
}

static async Task UpsertOneAsync(Container container, PhoneStatusInfo upsertOne)
{
    Console.WriteLine("\r\n====Upsert====");

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

        Console.WriteLine($"upsertResult.RequestCharge : {upsertResult.RequestCharge}");
        Console.WriteLine($"""
            upsertResult.Resource :
              Id: {upsertResult.Resource.Id}
              phone:{upsertResult.Resource.Phone}
              Comment:{upsertResult.Resource.Comment}
              History count:{upsertResult.Resource.History.Count}
            """);
    }
    catch (CosmosException ex)
    {
        Console.WriteLine($"UpsertItemAsync got CosmosException occurred: StatusCode is {ex.StatusCode}, RequestCharge: {ex.RequestCharge}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"UpsertItemAsync got error occurred[{ex.GetType().FullName}]: {ex.Message}");
    }
}

static async Task PatchOneAsync(Container container, string id2Patch, string phone2Patch, PatchOperation[] patchOperations)
{
    Console.WriteLine("\r\n====Patching====");

    foreach (var item in patchOperations)
    {
        Console.WriteLine($"  # {item.OperationType} {item.Path}");
    }

    var patchResult = await container.PatchItemAsync<PhoneStatusInfo>(id2Patch, new PartitionKey(phone2Patch), patchOperations);

    Console.WriteLine($"patchResult.RequestCharge : {patchResult.RequestCharge}");
    Console.WriteLine($"""
              patchResult.Resource :
              Id: {patchResult.Resource.Id}
              phone:{patchResult.Resource.Phone}
              Comment:{patchResult.Resource.Comment}
              History count:{patchResult.Resource.History.Count}
            """);
}

static async Task DeleteItemsAsync(Container container, List<PhoneStatusInfo> item1)
{
    Console.WriteLine($"{Environment.NewLine}Press Enter to delete {item1.Count} items just newly created in this session, or any other key to exit.");
    var key = Console.ReadKey(true);

    if (key.Key != ConsoleKey.Enter) return;
    Console.WriteLine("\r\n====Deleting items====");

    double totalRequestCharges = 0;
    foreach (var item in item1)
    {
        var delResp = await container.DeleteItemAsync<PhoneStatusInfo>(item.Id, new PartitionKey(item.Phone));
        Console.WriteLine($"Delete {item.Id}/{item.Phone} took {delResp.RequestCharge} RUs");

        totalRequestCharges += delResp.RequestCharge;
    }

    Console.WriteLine($"Delete {item1.Count} took {totalRequestCharges} RUs");
}