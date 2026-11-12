namespace Cube.Api;

public sealed class Category : Entity {
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public Guid? ResponsibleUserId { get; set; }
}

public sealed class ServiceRequest : Owned
{
    public Guid CategoryId { get; set; }
    public Guid? PremisesId { get; set; }
    public Guid? AssigneeId { get; set; }
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "New";
    public DateTimeOffset? DueAt { get; set; }
    public uint Version { get; set; }
}
public sealed class RequestMessage : Owned {
    public Guid RequestId { get; set; }
    public Guid AuthorId { get; set; }
    public string Text { get; set; } = "";
    public bool Internal { get; set; }
}

public sealed class RequestEvent : Owned {
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string Reason { get; set; } = ""; }
public sealed class Attachment : Owned {
    public Guid FileId { get; set; }
    public Guid? RequestId { get; set; }
    public Guid? MessageId { get; set; }
    }
