using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDayAPI.Services
{
    // Uploads images to Azure Blob Storage. The connection string comes from configuration
    // (user secrets locally, an environment variable when deployed) and is never in the code.
    public class BlobImageStorage : IImageStorage
    {
        private readonly string? _connectionString;
        private readonly string _containerName;

        public BlobImageStorage(IConfiguration configuration)
        {
            _connectionString = configuration["AzureStorage:ConnectionString"];
            _containerName = configuration["AzureStorage:ContainerName"] ?? "raceday-images";
        }

        public async Task<string> SaveAsync(IFormFile file, string folder)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Azure Storage is not configured.");
            }

            var container = new BlobContainerClient(_connectionString, _containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
            var blob = container.GetBlobClient(blobName);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

            return blob.Uri.ToString();
        }
    }
}