using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LearnAzureStorage;

record Message(string Text, DateTime DateCreated);
internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly BlobServiceClient _client;
    private readonly StorageOptions _options;

    public Worker(ILogger<Worker> logger, IOptions<StorageOptions> options, BlobServiceClient client)
    {
        _logger = logger;
        _client = client;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("We're going to save your input to Azure storage, Please 'GO' to continue.");
        var gather = Console.ReadLine();
        if ("go".Equals(gather, StringComparison.OrdinalIgnoreCase))
        {

            var container = _client.GetBlobContainerClient(_options.ContainerName);

            container!.CreateIfNotExists();

            string blobName = "test-object2.json";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Message("this is a test json object", DateTime.Now))));

            var blobClient = container.GetBlobClient(blobName);
            await blobClient.UploadAsync(stream, stoppingToken);

            //overwrite failed
            try
            {
                stream.Position = 0;
                await blobClient.UploadAsync(stream, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Expect exception without overwrite flag in parameter");
            }

            //overwrite failed
            stream.Position = 0;
            await blobClient.UploadAsync(stream, true, stoppingToken);
            _logger.LogInformation("Re-upload the file successfully with parameter overwrite = true");
        }


        await Task.CompletedTask;
    }
}
