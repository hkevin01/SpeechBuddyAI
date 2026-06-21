namespace Microsoft.Maui.Storage;

public static class FileSystem
{
    public static string AppDataDirectory { get; set; } = Path.GetTempPath();
}

namespace Microsoft.Maui.ApplicationModel.DataTransfer;

public interface IShare
{
    Task RequestAsync(ShareFileRequest request);
}

public sealed class ShareFile
{
    public ShareFile(string fullPath)
    {
        FullPath = fullPath;
    }

    public string FullPath { get; }
}

public sealed class ShareFileRequest
{
    public string? Title { get; set; }
    public ShareFile? File { get; set; }
}

public static class Share
{
    public static IShare Default { get; set; } = new NoOpShare();

    private sealed class NoOpShare : IShare
    {
        public Task RequestAsync(ShareFileRequest request)
        {
            return Task.CompletedTask;
        }
    }
}
