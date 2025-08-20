using API.Util;

namespace API.Endpoints;

public static class Download
{
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/download/{fileName}", async (string fileName) =>
        {
            var blobFolder = RetrieveBlobFolder.Get();
            var filePath = Path.Combine(blobFolder, fileName);

            if (!File.Exists(filePath)) return Results.NotFound("File not found.");

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            return Results.File(fileBytes, "application/octet-stream", fileName);
        });
    }
}