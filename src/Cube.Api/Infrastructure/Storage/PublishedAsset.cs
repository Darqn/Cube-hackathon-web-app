namespace Cube.Api;

public sealed class PublishedAsset : Entity
{
    public Guid? ListingId { get; set; }
    public Guid? NewsId { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string State { get; set; } = "Quarantined";
}
