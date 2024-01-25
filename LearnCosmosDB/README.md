# LearnCosmosDB

In this demo, we use connection string to connect CosmosDB. Usually, we should reference `Azure.Identity`.

## Prepare
You can use  https://cosmos.azure.com/try to create "Azure Cosmos DB for NoSQL (recommended)" firstly and get the connection string, and put it in `appSettings.json`.

You can add a applicationInsights connection string in `appSettings.json` to use application insights.

### CosmosDB emulator
1. Run the emulator docker container
1. 
```
docker run --name cosmos-emulator `
    --publish 18081:8081 `
    --publish 10250-10255:10250-10255 `
    --interactive `
    --tty `
    mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

2. Export the certificate and install it in windows: 
```
curl -k https://localhost:8081/_explorer/emulator.pem > ~/emulatorcert.crt
```

3. Code snippets
```
using CosmosClient client = new(
    accountEndpoint: "https://localhost:8081/",
    authKeyOrResourceToken: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
);
```

But when using CosmosDB emulator, we got timeout issue with following exceptions after a lot of minutes: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1635
```
System.Net.Http.HttpRequestException: 'A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond. (172.17.0.2:8081)'

Innter Exception:
SocketException: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.
```

## Running logs
```
account.Id: cosmosrgeastusfab59b96-8b73-482b-bbaedb; ReadableRegions: East US; account.Consistency: Session
database.Id : CARE
container.Id : PhoneStatusBook

====Create Items with ttl 120 ====
Create [95ff678c-edb0-4f23-a14e-31eb0d9c45c1,6268889999] with ttl 120, result.StatusCode : Created, result.RequestCharge : 9.52
Create [429de019-6625-4bbc-8599-dfb003b631f9,6268889999] with ttl 120, result.StatusCode : Created, result.RequestCharge : 9.52

====Create Items ====
Create [d6c3d0f7-5ef3-4f37-a853-35ba02d60033,10104101437] result.StatusCode : Created, result.RequestCharge : 9.14
Create [77d59690-d8d4-4b25-94ea-ddd6b7237021,10104101437] result.StatusCode : Created, result.RequestCharge : 9.14
Create [9fb2443a-04ad-466b-8eaa-b4e80b4c6831,10104101437] result.StatusCode : Created, result.RequestCharge : 9.14

====Search Phone ====
[Start query]:  d6c3d0f7-5ef3-4f37-a853-35ba02d60033, 10104101437, Biz-A, 1/4/2024 10:14:40 AM, charged: [2.89]
[Start query]:  77d59690-d8d4-4b25-94ea-ddd6b7237021, 10104101437, Biz-B, 1/4/2024 10:14:40 AM, charged: [2.89]
[Start query]:  9fb2443a-04ad-466b-8eaa-b4e80b4c6831, 10104101437, Biz-C, 1/4/2024 10:14:40 AM, charged: [2.89]
totalRequestChange RUs for search phone: 8.67

====Read Item for id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831 and phone '10104101437'====
readResult.RequestCharge : 1
readResult.Resource :
  Id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831
  phone:10104101437
  Comment:
  History count:1

====Upsert====
upsertResult.RequestCharge : 13.71
upsertResult.Resource :
  Id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831
  phone:10104101437
  Comment:Updated: 1/4/2024 10:14:44 AM
  History count:0

====Read Item for id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831 and phone '10104101437'====
readResult.RequestCharge : 1
readResult.Resource :
  Id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831
  phone:10104101437
  Comment:Updated: 1/4/2024 10:14:44 AM
  History count:0

====Patching====
  # Set /lineOfBiz
  # Set /status
  # Set /comment
  # Add /history/0
  # Add /history/0
  # Add /history/0
patchResult.RequestCharge : 15.03
  patchResult.Resource :
  Id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831
  phone:10104101437
  Comment:Updated: 1/4/2024 10:14:47 AM, with new status: Grey
  History count:3

====Patching====
  # Set /status
  # Add /history/0
patchResult.RequestCharge : 11.68
  patchResult.Resource :
  Id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831
  phone:10104101437
  Comment:Updated: 1/4/2024 10:14:47 AM, with new status: Grey
  History count:4

Press Enter to remove history[1], or any other key to continue.

====Patching====
  # Set /comment
  # Remove /history/1
patchResult.RequestCharge : 11.68
  patchResult.Resource :
  Id: 9fb2443a-04ad-466b-8eaa-b4e80b4c6831
  phone:10104101437
  Comment:Updated: 1/4/2024 10:15:26 AM, with history[1] removed
  History count:3

Press Enter to delete 3 items just newly created in this session, or any other key to exit.

====Deleting items====
Delete d6c3d0f7-5ef3-4f37-a853-35ba02d60033/10104101437 took 9.14 RUs
Delete 77d59690-d8d4-4b25-94ea-ddd6b7237021/10104101437 took 9.14 RUs
Delete 9fb2443a-04ad-466b-8eaa-b4e80b4c6831/10104101437 took 10.48 RUs
Delete 3 took 28.76 RUs

[End] Exited
```


# References
- https://learn.microsoft.com/en-us/azure/cosmos-db/request-units
- https://learn.microsoft.com/en-us/training/modules/build-dotnet-app-azure-cosmos-db-nosql/
- https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/quickstart-dotnet
- https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-dotnet-get-started
- https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection

cosmos db emulator
- https://learn.microsoft.com/en-us/azure/cosmos-db/emulator
- https://cosmos.azure.com/try
- https://cosmos.azure.com/sunset/

applicationInsights
- https://learn.microsoft.com/en-us/azure/azure-monitor/app/ilogger?tabs=dotnet6

# LearnKeyVault

## Preparation
In local running please following environment variables in `launchSettings.json` with your values in Azure:
```
        //"AZURE_CLIENT_ID": "00000000-0000-0000-0000-000000000000",
        //"AZURE_TENANT_ID": "00000000-0000-0000-0000-000000000000",
        //"AZURE_CLIENT_SECRET": ""
```

## Demo contains
1. read value from key vault for key "DefaultCosmosDBConnectionString"
1. overwrite appsetting's value for key "Key02" with value from KeyVault "LearnKeyVault-Key02", the prefix got set as "LearnKeyVault" here.