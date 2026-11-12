namespace Cube.Api;

public sealed class Document : Owned
{
    public Guid FileId
    {
        get; set;
    }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "Contract";
    public string Number { get; set; } = "";
    public DateOnly DocumentDate
    {
        get; set;
    }
    public Guid? LeaseId
    {
        get; set;
    }
    public Guid? PremisesId
    {
        get; set;
    }
    public bool Archived
    {
        get; set;
    }
    public string Status { get; set; } = "Published";
}
