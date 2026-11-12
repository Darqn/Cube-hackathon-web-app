namespace Cube.Api;

public sealed class Outbox : Entity
{
    public string Provider { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid ResourceId
    {
        get; set;
    }
    public string Status { get; set; } = "AwaitingConfiguration";
    public string? LastError
    {
        get; set;
    }
    public int Attempts
    {
        get; set;
    }
}
