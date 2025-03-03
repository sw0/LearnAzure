using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

            container!.CreateIfNotExists(cancellationToken: stoppingToken);

            string blobName = "test-object.json";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Message("this is a test json object", DateTime.Now))));

            var blobClient = container.GetBlobClient(blobName);

            if (await blobClient.ExistsAsync(stoppingToken))
            {
                //retrieve existing blob information
                var dlContentResp = await blobClient.DownloadContentAsync(stoppingToken);
                var body = Encoding.UTF8.GetString(dlContentResp.Value.Content);

                _logger.LogWarning("the object '{BlobName}' has existed, skipping initial upload. The content is: '{Content}'", blobName, body);


                var properties = await blobClient.GetPropertiesAsync(cancellationToken: stoppingToken);

                _logger.LogInformation("{BlobName}'s properties are: {Properties}; metadata: {Metadata}", blobName, new
                {
                    properties.Value.ContentType,
                    properties.Value.ContentLanguage,
                    properties.Value.ContentHash
                }, properties.Value.Metadata);
            }
            else
            {
                await blobClient.UploadAsync(stream, stoppingToken);
            }

            //overwrite failed
            try
            {
                stream.Position = 0;
                await blobClient.UploadAsync(stream, stoppingToken);
            }
            catch (Exception ex) when (ex is Azure.RequestFailedException afe)
            {
                _logger.LogError("Expect exception without overwrite flag in parameter, status: {Status}, Error: {Error}", afe.Status, afe.Message);
            }

            //overwrite successfully with overwrite flag = true
            stream.Position = 0;
            await blobClient.UploadAsync(stream, true, stoppingToken);
            _logger.LogInformation("Re-upload the file successfully with parameter overwrite = true");

            await SetRetrieveHeaders(blobClient);
            await UpdateMetadata(blobClient);
        }
    }

    async Task SetRetrieveHeaders(BlobClient blob)
    {
        // Get the existing properties
        BlobProperties properties = await blob.GetPropertiesAsync();

        BlobHttpHeaders headers = new BlobHttpHeaders
        {
            // Set the MIME ContentType every time the properties 
            // are updated or the field will be cleared
            ContentType = "text/plain",
            ContentLanguage = "en-us",

            // Populate remaining headers with 
            // the pre-existing properties
            CacheControl = properties.CacheControl,
            ContentDisposition = properties.ContentDisposition,
            ContentEncoding = properties.ContentEncoding,
            ContentHash = properties.ContentHash
        };


        // Set the blob's properties.
        await blob.SetHttpHeadersAsync(headers);
    }

    async Task UpdateMetadata(BlobClient blob)
    {
        IDictionary<string, string> metadata =
           new Dictionary<string, string>
           {
               // Add metadata to the dictionary by calling the Add method
               { "docType", "textDocuments" }
           };

        // Add metadata to the dictionary by using key/value syntax
        metadata["category"] = "guidance";
        metadata["docSid"] = "DOC202503030001";

        // Set the blob's metadata.
        await blob.SetMetadataAsync(metadata);
    }
}
