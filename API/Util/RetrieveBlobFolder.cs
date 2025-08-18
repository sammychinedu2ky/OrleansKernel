namespace API.Util;

public static class RetrieveBlobFolder
{
    public static string Get()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var blobFolder = Path.Combine(projectRoot, "BlobStorage");
        return blobFolder;
    }
}