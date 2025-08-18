using API.Hubs;

namespace API.Grains;

public interface IFileSavingGrain : IGrainWithGuidKey
{
    Task<List<FileMessage>> SaveFiles(IFormFileCollection formFiles);
    Task<FileMessage> SaveFile(string fileName, Stream fileDataStream);
}

public class FileSavingGrain : Grain, IFileSavingGrain
{
    private readonly ILogger<FileSavingGrain> _logger;

    public FileSavingGrain(ILogger<FileSavingGrain> logger)
    {
        _logger = logger;
    }
    public async Task<FileMessage> SaveFile(string fileName, Stream fileDataStream)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var blobFolder = Path.Combine(projectRoot, "BlobStorage");
        // creates folder if it doesn't exist
        Directory.CreateDirectory(blobFolder);

        // Simulate saving the file and generating a file ID
        var fileId = Guid.NewGuid().ToString();
        var filePath = Path.Combine(blobFolder, fileId);

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileDataStream.CopyToAsync(fileStream);
        }

        return new FileMessage
        {
            FileId = fileId,
            FileName = fileName,
            FileType = Path.GetExtension(fileName)
        };
    }
    public async Task<List<FileMessage>> SaveFiles(IFormFileCollection formFiles)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var blobFolder = Path.Combine(projectRoot, "BlobStorage");
        // creates folder if it doens't exist
        Directory.CreateDirectory(blobFolder);

        // Simulate saving the file and generating a file ID
        var fileMessages = new List<FileMessage>();
        foreach (var formFile in formFiles)
        {
            if (formFile.Length > 0)
            {
                var fileId = Guid.NewGuid().ToString();
                var filePath = Path.Combine(blobFolder, fileId);
                
                using var stream = formFile.OpenReadStream();
                using var fileStream = new FileStream(filePath, FileMode.Create);
                await stream.CopyToAsync(fileStream);

                var fileMessage = new FileMessage
                {
                    FileId = fileId,
                    FileName = formFile.FileName,
                    FileType = formFile.ContentType
                };
                
                fileMessages.Add(fileMessage);
            }
            else
            {
                _logger.LogWarning("Received an empty file: {FileName}", formFile.FileName);
            }
        }
        return fileMessages;
    }
}
