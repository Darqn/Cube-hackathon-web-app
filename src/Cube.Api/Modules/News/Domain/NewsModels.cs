namespace Cube.Api;

public sealed class News : Entity
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "General";
    public string Importance { get; set; } = "Normal";
    public DateTimeOffset? PublishAt
    {
        get; set;
    }
    public DateTimeOffset? ExpiresAt
    {
        get; set;
    }
    public bool Archived
    {
        get; set;
    }
    public bool AllTenants
    {
        get; set;
    }
    public bool RequiresAcknowledgement
    {
        get; set;
    }
    public Guid[] TenantIds { get; set; } = [];
    public Guid[] PremisesIds { get; set; } = [];
    public string? Building
    {
        get; set;
    }
    public string? Section
    {
        get; set;
    }
    public int? Floor
    {
        get; set;
    }
}
public sealed class NewsReceipt : Owned
{
    public Guid NewsId
    {
        get; set;
    }
    public Guid UserId
    {
        get; set;
    }
    public DateTimeOffset? ReadAt
    {
        get; set;
    }
    public DateTimeOffset? AcknowledgedAt
    {
        get; set;
    }
}
