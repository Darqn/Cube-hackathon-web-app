namespace Cube.Api;

public sealed class Audit : Entity {
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public Guid ResourceId { get; set; }
    }
