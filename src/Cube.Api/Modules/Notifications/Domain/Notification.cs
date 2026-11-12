namespace Cube.Api;

public sealed class Notification : Owned
{
    public string? DeduplicationKey
    {
        get; set;
    }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public Guid? ResourceId
    {
        get; set;
    }
    public Guid? UserId
    {
        get; set;
    }
    public bool Read
    {
        get; set;
    }
}
