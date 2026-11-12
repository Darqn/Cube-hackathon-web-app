namespace Cube.Api;

public sealed class StoredFile : Owned {
    public Guid UploadedBy { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string State { get; set; } = "Quarantined"; }
