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
```
account.Id: cosmosrgeastusfab59b96-8b73-482b-bbaedb; ReadableRegions: East US; account.Consistency: Session
database.Id : CARE
container.Id : PhoneStatusBook

====Create Items ====
Create [59e783f9-e87c-416c-8cb9-c6cf1ed1fbd6,10103225913] result.StatusCode : Created, result.RequestCharge : 8.76
Create [ca490360-7f93-4090-b442-3b2dfba974c0,10103225913] result.StatusCode : Created, result.RequestCharge : 8.76
Create [b779d815-ca06-4afb-bcc8-847138cf9d2a,10103225913] result.StatusCode : Created, result.RequestCharge : 8.76

====Search Phone ====
[Start query]:  ca490360-7f93-4090-b442-3b2dfba974c0, 10103225913, Biz-B, 1/3/2024 10:59:16 PM, charged: [2.87]
[Start query]:  b779d815-ca06-4afb-bcc8-847138cf9d2a, 10103225913, Biz-C, 1/3/2024 10:59:16 PM, charged: [2.87]
totalRequestChange RUs for search phone: 5.74

====Read Item for id: b779d815-ca06-4afb-bcc8-847138cf9d2a and phone '10103225913'====
readResult.RequestCharge : 1
upsertResult1.Resource :
  Id: b779d815-ca06-4afb-bcc8-847138cf9d2a
  phone:10103225913
  Comment:
  History count:1

====Upsert====
upsertResult1.RequestCharge : 13.71
upsertResult1.Resource :
  Id: b779d815-ca06-4afb-bcc8-847138cf9d2a
  phone:10103225913
  Comment:Updated: 1/3/2024 10:59:24 PM
  History count:0

====Read Item for id: b779d815-ca06-4afb-bcc8-847138cf9d2a and phone '10103225913'====
readResult.RequestCharge : 1
upsertResult1.Resource :
  Id: b779d815-ca06-4afb-bcc8-847138cf9d2a
  phone:10103225913
  Comment:Updated: 1/3/2024 10:59:24 PM
  History count:0

====Patching====
patchResult1.RequestCharge : 13.64
  patchResult1.Resource :
  Id: b779d815-ca06-4afb-bcc8-847138cf9d2a
  phone:10103225913
  Comment:Updated: 1/3/2024 10:59:28 PM, with new status: Grey
  History count:1

====Patching====
patchResult1.RequestCharge : 11.87
  patchResult1.Resource :
  Id: b779d815-ca06-4afb-bcc8-847138cf9d2a
  phone:10103225913
  Comment:Updated: 1/3/2024 10:59:28 PM, with new status: Grey
  History count:2

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

