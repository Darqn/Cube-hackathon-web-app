using Cube.Api.Common.Storage;

namespace Cube.Api.Infrastructure.Storage;

public sealed record FileWrite(string Key, byte[] Bytes);
public sealed record ValidatedUpload(string Name, string ContentType, byte[] Bytes);

// Coordinates the file side of a DB commit, including partial writes and cancellation.
public sealed class FileWriteBatch(IPrivateFileStore files, ILogger<FileWriteBatch> logger)
{
    public async Task SaveAsync(IEnumerable<FileWrite> writes, Func<CancellationToken, Task> commit, CancellationToken ct)
    {
        var attempted = new List<string>();
        try
        {
            foreach (var write in writes)
            {
                attempted.Add(write.Key);
                await files.WriteAsync(write.Key, write.Bytes, ct);
            }
            await commit(ct);
        }
        catch
        {
            foreach (var key in attempted)
            {
                try { files.Delete(key); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Preserve the original failure; never log the filename or uploaded content.
                    logger.LogError("Could not clean up a failed private upload: {ExceptionType}", error.GetType().Name);
                }
            }
            throw;
        }
    }
}
