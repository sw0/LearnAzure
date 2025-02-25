// See https://aka.ms/new-console-template for more information

using Azure.Core;
using Azure.Identity;
using LearnAzure.Common;

Console.WriteLine("Welcome to LearnAzureStorage Demo");


var builder = Host.CreateApplicationBuilder();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.AddAzureClients(clientBuilder =>
{
    var configuration = builder.Configuration;

    var tenantId = configuration[Constants.AZURE_TENANT_ID];
    var clientId = configuration[Constants.AZURE_CLIENT_ID];
    var clientSecretId = configuration[Constants.AZURE_CLIENT_SECRET];

    TokenCredential credental = new ClientSecretCredential(tenantId, clientId, clientSecretId);

    var storageAccount = builder.Configuration["AzureStorage:AccountName"];

    clientBuilder.AddBlobServiceClient(new Uri($"https://{storageAccount}.blob.core.windows.net"))
    .WithCredential(credental ?? new DefaultAzureCredential());


});

builder.Services.AddHostedService<Worker>();


builder.Build().Run();