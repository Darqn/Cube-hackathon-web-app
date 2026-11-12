namespace Cube.Api;

public sealed class Tenant : Entity
{
    public string Name { get; set; } = "";
    public string Inn { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool Blocked
    {
        get; set;
    }
}

public sealed class Premises : Entity
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Office";
    public string Address { get; set; } = "";
    public string Building { get; set; } = "";
    public string Section { get; set; } = "";
    public int? Floor
    {
        get; set;
    }
    public decimal Area
    {
        get; set;
    }
    public decimal MonthlyRent
    {
        get; set;
    }
    public string Description { get; set; } = "";
    public string Utilities { get; set; } = "";
    public string Status { get; set; } = "Available";
}
public sealed class Lease : Owned
{
    public Guid PremisesId
    {
        get; set;
    }
    public string Number { get; set; } = "";
    public DateOnly SignedOn
    {
        get; set;
    }
    public DateOnly StartsOn
    {
        get; set;
    }
    public DateOnly EndsOn
    {
        get; set;
    }
    public string Status { get; set; } = "Active";
}
public sealed class Listing : Entity
{
    public Guid PremisesId
    {
        get; set;
    }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Published
    {
        get; set;
    }
    public bool Archived
    {
        get; set;
    }
}

public sealed class ListingInquiry : Owned
{
    public Guid ListingId
    {
        get; set;
    }
    public Guid RequestId
    {
        get; set;
    }
}
