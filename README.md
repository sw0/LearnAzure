# LearnCosmosDB

In this demo, we use connection string to connect CosmosDB. Usually, we should reference `Azure.Identity`.

## Prepare
You can use  https://cosmos.azure.com/try to create "Azure Cosmos DB for NoSQL (recommended)" firstly and get the connection string, and put it in `appSettings.json`.

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
```account.Id: cosmosrgeastusfab59b96-8b73-482b-bbaedb; ReadableRegions: East US; account.Consistency: Session
database.Id : CARE
container.Id : PhoneStatusBook

====Create Items ====
Create [846531f0-659a-413f-b9bd-3390ebac8cec,10104000022] result.StatusCode : Created, result.RequestCharge : 8.76
Create [63178777-799d-4223-9b2b-e82018dda477,10104000022] result.StatusCode : Created, result.RequestCharge : 8.76
Create [8a774600-93e2-42ca-baab-5ec645d83f7d,10104000022] result.StatusCode : Created, result.RequestCharge : 8.76

====Search Phone ====
[Start query]:  846531f0-659a-413f-b9bd-3390ebac8cec, 10104000022, Biz-A, 1/4/2024 12:00:22 AM, charged: [2.89]
[Start query]:  63178777-799d-4223-9b2b-e82018dda477, 10104000022, Biz-B, 1/4/2024 12:00:25 AM, charged: [2.89]
[Start query]:  8a774600-93e2-42ca-baab-5ec645d83f7d, 10104000022, Biz-C, 1/4/2024 12:00:25 AM, charged: [2.89]
totalRequestChange RUs for search phone: 8.67

====Read Item for id: 8a774600-93e2-42ca-baab-5ec645d83f7d and phone '10104000022'====
readResult.RequestCharge : 1
readResult.Resource :
  Id: 8a774600-93e2-42ca-baab-5ec645d83f7d
  phone:10104000022
  Comment:
  History count:1

====Upsert====
upsertResult.RequestCharge : 13.71
upsertResult.Resource :
  Id: 8a774600-93e2-42ca-baab-5ec645d83f7d
  phone:10104000022
  Comment:Updated: 1/4/2024 12:00:29 AM
  History count:0

====Read Item for id: 8a774600-93e2-42ca-baab-5ec645d83f7d and phone '10104000022'====
readResult.RequestCharge : 1
readResult.Resource :
  Id: 8a774600-93e2-42ca-baab-5ec645d83f7d
  phone:10104000022
  Comment:Updated: 1/4/2024 12:00:29 AM
  History count:0

====Patching====
  # Set /lineOfBiz
  # Set /status
  # Set /comment
  # Add /history/0
patchResult.RequestCharge : 13.64
  patchResult.Resource :
  Id: 8a774600-93e2-42ca-baab-5ec645d83f7d
  phone:10104000022
  Comment:Updated: 1/4/2024 12:00:32 AM, with new status: Grey
  History count:1

====Patching====
  # Set /status
  # Add /history/0
patchResult.RequestCharge : 11.87
  patchResult.Resource :
  Id: 8a774600-93e2-42ca-baab-5ec645d83f7d
  phone:10104000022
  Comment:Updated: 1/4/2024 12:00:32 AM, with new status: Grey
  History count:2

Press Enter to delete 3 items just newly created in this session, or any other key to exit.

====Deleting items====
Delete 846531f0-659a-413f-b9bd-3390ebac8cec/10104000022 took 8.76 RUs
Delete 63178777-799d-4223-9b2b-e82018dda477/10104000022 took 8.76 RUs
Delete 8a774600-93e2-42ca-baab-5ec645d83f7d/10104000022 took 9.52 RUs
Delete 3 took 27.04 RUs

[End] Exited

```


# References
- https://learn.microsoft.com/en-us/training/modules/build-dotnet-app-azure-cosmos-db-nosql/
- https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/quickstart-dotnet
- https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-dotnet-get-started
- https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection

cosmos db emulator
- https://learn.microsoft.com/en-us/azure/cosmos-db/emulator
- https://cosmos.azure.com/try
- https://cosmos.azure.com/sunset/

