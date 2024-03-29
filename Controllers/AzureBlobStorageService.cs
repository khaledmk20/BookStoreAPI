using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;


public class AzureBlobStorageService
{

    private readonly IConfiguration _config;
    public AzureBlobStorageService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, bool resize)
    {
        if (!contentType.StartsWith("image/"))
            throw new ArgumentException("The provided file is not an image.");
        var compressedStream = fileStream;
        if (resize == true)
        {
            using (var image = Image.Load(fileStream))
            {
                image.Mutate(x => x.Resize(256, 256));


                compressedStream = new MemoryStream();
                image.Save(compressedStream, new JpegEncoder { Quality = 100 });
                compressedStream.Seek(0, SeekOrigin.Begin);
            }

        }


        var container = new BlobContainerClient(_config.GetConnectionString("AzureStorage"), "bookstore");
        var createResponse = await container.CreateIfNotExistsAsync();
        if (createResponse != null && createResponse.GetRawResponse().Status == 201)
            await container.SetAccessPolicyAsync(PublicAccessType.Blob);
        var blob = container.GetBlobClient(fileName);

        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

        await blob.UploadAsync(resize == true ? compressedStream : fileStream, new BlobHttpHeaders { ContentType = contentType });

        return blob.Uri.ToString();
    }
}


