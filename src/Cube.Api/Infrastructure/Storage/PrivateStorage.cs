using System.IO.Compression;

namespace Cube.Api;

public sealed class PrivateStorage(IConfiguration config, IWebHostEnvironment environment) : Cube.Api.Common.Storage.IPrivateFileStore
{
    public Task WriteAsync(string key, byte[] bytes, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        return File.WriteAllBytesAsync(PathFor(key), bytes, ct);
    }
    public void Delete(string key) => File.Delete(PathFor(key));
    public string Root { get; } = Path.GetFullPath(config["Files:Root"] ?? Path.Combine(environment.ContentRootPath, ".data", "files"));
    public bool AllowDevelopmentFiles => environment.IsDevelopment() && config.GetValue<bool>("Files:AllowUnscannedDevelopmentUploads");
    public string PathFor(string key)
    {
        Guard.That(key.Length == 32 && key.All(Uri.IsHexDigit), "invalid_storage_key");
        return Path.Combine(Root, key);
    }
    public static string Validate(byte[] bytes, string filename)
    {
        Guard.That(bytes.Length > 0 && bytes.Length <= 20 * 1024 * 1024, "invalid_file_size", 413);
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        bool Is(params byte[] prefix) => bytes.AsSpan().StartsWith(prefix);
        return ext switch
        {
            ".pdf" when Is(37, 80, 68, 70, 45) => "application/pdf",
            ".jpg" or ".jpeg" when Is(255, 216, 255) => "image/jpeg",
            ".png" when Is(137, 80, 78, 71, 13, 10, 26, 10) => "image/png",
            ".doc" when Is(208, 207, 17, 224, 161, 177, 26, 225) => "application/msword",
            ".xls" when Is(208, 207, 17, 224, 161, 177, 26, 225) => "application/vnd.ms-excel",
            ".docx" or ".xlsx" when Is(80, 75, 3, 4) => ValidateOffice(bytes, ext),
            _ => throw new ApiError(400, "unsupported_or_invalid_file")
        };
    }
    private static string ValidateOffice(byte[] bytes, string extension)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            Guard.That(zip.Entries.Count <= 2000 && zip.Entries.Sum(x => x.Length) <= 100 * 1024 * 1024, "unsafe_office_archive");
            Guard.That(zip.Entries.All(x => !x.FullName.Contains("..") && !x.FullName.StartsWith('/') && !x.FullName.Contains('\\') && !x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)), "unsafe_office_archive");
            Guard.That(zip.GetEntry("[Content_Types].xml") is not null && zip.GetEntry(extension == ".docx" ? "word/document.xml" : "xl/workbook.xml") is not null, "invalid_office_document");
            return extension == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }
        catch (InvalidDataException) { throw new ApiError(400, "invalid_office_document"); }
    }
}
