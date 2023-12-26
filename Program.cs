using LearnCosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;

var configurationBuilder = new ConfigurationBuilder();
configurationBuilder.AddJsonFile("appsettings.json", true)
    .AddUserSecrets<Program>();

var configuration = configurationBuilder.Build();

var connectionString = configuration.GetConnectionString("COSMOSDB_DEFAULT");

if (string.IsNullOrEmpty(connectionString) || connectionString == "<TO_BE_SET>")
{
    Console.WriteLine("Please set the connection string for CosmosDB");
    return;
}

CosmosSerializationOptions serializerOptions = new()
{
    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
};

CosmosClientOptions options = new()
{
    ConnectionMode = ConnectionMode.Gateway
};
//using CosmosClient client = new(connectionString, options); 
using CosmosClient client = new CosmosClientBuilder(connectionString).WithConnectionModeGateway()
    .WithSerializerOptions(serializerOptions)
    .Build();

var account = await client.ReadAccountAsync();

Console.WriteLine($"account.Id: {account.Id}; ReadableRegions: {string.Join(",", account.ReadableRegions.Select(x => x.Name))}; account.Consistency: {account.Consistency}");

Database database = await client.CreateDatabaseIfNotExistsAsync("CARE");

Console.WriteLine($"database.Id : {database.Id}");

Container container = await database.CreateContainerIfNotExistsAsync(configuration["CollectionName"] ?? "PhoneStatusInfo", "/phone", 400);
Console.WriteLine($"container.Id : {container.Id}");


Console.WriteLine("Press 'Enter' to create the documents, or other keys to continue...");
ConsoleKeyInfo key = Console.ReadKey();
if (key.Key == ConsoleKey.Enter)
{
    Console.WriteLine("\r\n====Create Items ====");

    var phone = $"1{DateTime.Now:MMddHHmmss}";

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


var phone2Search = configuration["Phone2Search"];
if (!string.IsNullOrEmpty(phone2Search))
{
    Console.WriteLine("\r\n====Search Phone ====");

    var queryable = container.GetItemLinqQueryable<PhoneStatusInfo>();
    var matches = queryable.Where(p => p.Phone == phone2Search).OrderBy(p => p.CreateDate).Skip(1);
    using var linqFeed = matches.ToFeedIterator();

    double totalRequestChange = 0;
    while (linqFeed.HasMoreResults)
    {
        var resp = await linqFeed.ReadNextAsync();

        foreach (var item in resp)
        {
            Console.WriteLine($"[Start query]:\t{item.Id}, {item.Phone}, {item.LineOfBiz}, {item.CreateDate}, charged: [{resp.RequestCharge}]");

            totalRequestChange += resp.RequestCharge;
        }
    }
    Console.WriteLine($"totalRequestChange RUs for search phone: {totalRequestChange}");
}

await Task.Delay(500);

var id2Read = configuration["Id2Read"];
var phone2Read = configuration["Phone2Read"];

if(!string.IsNullOrEmpty(phone2Read) && !string.IsNullOrEmpty(id2Read))
{
    Console.WriteLine($"\r\n====Read Item for id: {id2Read} and phone '{phone2Read}'====");

    try
    {
        var readResult = await container.ReadItemAsync<PhoneStatusInfo>(id2Read, new PartitionKey(phone2Read));

        Console.WriteLine($"readResult.RequestCharge : {readResult.RequestCharge}");
        Console.WriteLine($"readResult.Resource : {readResult.Resource}");
    }
    catch(CosmosException ex)
    {
        Console.WriteLine($"ReadItem got CosmosException occurred: StatusCode is {ex.StatusCode}, RequestCharge: {ex.RequestCharge}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ReadItem got error occurred[{ex.GetType().FullName}]: {ex.Message}");
    }
}
else
{
    Console.WriteLine("If you want to run read item, please set configuraton for 'Id2Read' and 'Phone2Read'\r\n");
}


//Console.WriteLine("\r\n====Upsert====");

//var upsertData_1 = new PhoneStatusInfo()
//{
//	id = "29fe6082-99fe-40e8-bf67-93f7bf46e405",
//	Phone = "18231226170153",
//	Status = PhoneStatus.Clear,
//	Comment = $"Updated: {DateTime.Now}",
//	History = new List<PhoneStatusRow>(),
//	CreateDate = DateTime.Parse("2023-12-26T17:01:53.8085827+08:00")
//};

//var upsertResult1 = await container.UpsertItemAsync<PhoneStatusInfo>(upsertData_1);

//Console.WriteLine($"upsertResult1.RequestCharge : {upsertResult1.RequestCharge}");
//Console.WriteLine($"upsertResult1.Resource : {upsertResult1.Resource}");

//await Task.Delay(500);

//Console.WriteLine("Press any key to patch the document");
//Console.ReadKey();
//Console.WriteLine("\r\n====Patch====");

//var patch = new PhoneStatusInfo()
//{
//	id = "29fe6082-99fe-40e8-bf67-93f7bf46e405",
//	Phone = "18231226170153"
//};

//var newStatus = PhoneStatus.Grey;

//var patchResult1 = await container.PatchItemAsync<PhoneStatusInfo>(patch.id, new PartitionKey(patch.Phone), new[]
//{
//	PatchOperation.Set("/Status", newStatus),
//	PatchOperation.Set("/Comment", $"Updated: {DateTime.Now}, with new status: {newStatus}"),
//	PatchOperation.Add("/History/0", new PhoneStatusRow(){CreateDate = DateTime.Now, Status = newStatus}),
//});

//Console.WriteLine($"patchResult1.RequestCharge : {patchResult1.RequestCharge}");
//Console.WriteLine($"patchResult1.Resource : {patchResult1.Resource}");











