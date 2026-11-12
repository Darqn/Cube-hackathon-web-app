using Cube.Api.Infrastructure.Storage;

namespace Cube.Api.Api.Files;

public static class UploadedFileReader
{
    public static async Task<ValidatedUpload> ReadAsync(IFormFile upload, CancellationToken ct)
    {
        Guard.That(upload.Length > 0 && upload.Length <= 20 * 1024 * 1024, "invalid_file_size", 413);
        var name = Path.GetFileName(upload.FileName);
        Guard.That(name.Length is > 0 and <= 200 && !name.Any(char.IsControl), "invalid_filename");
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        return new ValidatedUpload(name, PrivateStorage.Validate(bytes, name), bytes);
    }
}
