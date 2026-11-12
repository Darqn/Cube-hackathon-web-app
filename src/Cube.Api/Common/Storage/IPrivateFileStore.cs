namespace Cube.Api.Common.Storage;

public interface IPrivateFileStore
{
    Task WriteAsync(string key, byte[] bytes, CancellationToken ct);
    void Delete(string key);
}
