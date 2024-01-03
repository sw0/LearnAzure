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

Container container = await database.CreateContainerIfNotExistsAsync(configuration["CollectionName"] ?? "PhoneStatusInfo", "/phone", 400);
//Container container = await database.CreateContainerAsync(configuration["CollectionName"] ?? "PhoneStatusInfo", "/phone", 400);
Console.WriteLine($"container.Id : {container.Id}");

var phone = $"1{DateTime.Now:MMddHHmmss}";

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

await PatchOneAsync(container, patchOne.Id, patchOne.Phone);
await Task.Delay(1200);

await PatchOneAgainAsync(container, patchOne.Id, patchOne.Phone);
await Task.Delay(200);

static async Task CreateDemoItemsAsync(Container container, string phone)
{
    Console.WriteLine("\r\n====Create Items ====");

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
    Console.WriteLine("\r\n====Search Phone ====");

    var queryable = container.GetItemLinqQueryable<PhoneStatusInfo>();
    var matches = queryable.Where(p => p.Phone == phone2Search).OrderBy(p => p.CreateDate).Skip(1);
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
            upsertResult1.Resource :
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

        var upsertResult1 = await container.UpsertItemAsync<PhoneStatusInfo>(upsertData_1);

        Console.WriteLine($"upsertResult1.RequestCharge : {upsertResult1.RequestCharge}");
        Console.WriteLine($"""
            upsertResult1.Resource :
              Id: {upsertResult1.Resource.Id}
              phone:{upsertResult1.Resource.Phone}
              Comment:{upsertResult1.Resource.Comment}
              History count:{upsertResult1.Resource.History.Count}
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

static async Task PatchOneAsync(Container container, string id2Patch, string phone2Patch)
{
    Console.WriteLine("\r\n====Patching====");

    var newStatus = PhoneStatus.Grey;

    var patchResult1 = await container.PatchItemAsync<PhoneStatusInfo>(id2Patch, new PartitionKey(phone2Patch), new[]
    {
            PatchOperation.Set("/lineOfBiz", "test"),
            PatchOperation.Set("/status", newStatus),
            PatchOperation.Set("/comment", $"Updated: {DateTime.Now}, with new status: {newStatus}"),
            PatchOperation.Add("/history/0", new PhoneStatusRow(){CreateDate = DateTime.Now, Status = newStatus}),
        });

    Console.WriteLine($"patchResult1.RequestCharge : {patchResult1.RequestCharge}");
    Console.WriteLine($"""
              patchResult1.Resource :
              Id: {patchResult1.Resource.Id}
              phone:{patchResult1.Resource.Phone}
              Comment:{patchResult1.Resource.Comment}
              History count:{patchResult1.Resource.History.Count}
            """);
}

static async Task PatchOneAgainAsync(Container container, string id2Patch, string phone2Patch)
{
    Console.WriteLine("\r\n====Patching====");

    var newStatus = PhoneStatus.Clear;

    var patchResult1 = await container.PatchItemAsync<PhoneStatusInfo>(id2Patch, new PartitionKey(phone2Patch), new[]
    {
            PatchOperation.Set("/status", newStatus),
            PatchOperation.Add("/history/0", new PhoneStatusRow(){CreateDate = DateTime.Now, Status = newStatus}),
        });

    Console.WriteLine($"patchResult1.RequestCharge : {patchResult1.RequestCharge}");
    Console.WriteLine($"""
              patchResult1.Resource :
              Id: {patchResult1.Resource.Id}
              phone:{patchResult1.Resource.Phone}
              Comment:{patchResult1.Resource.Comment}
              History count:{patchResult1.Resource.History.Count}
            """);
}