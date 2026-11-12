namespace Cube.Api;

public abstract class Entity {
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public abstract class Owned : Entity {
    public Guid TenantId { get; set; }
    }
