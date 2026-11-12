namespace Cube.Api;

public sealed class Meter : Owned
{
    public Guid PremisesId
    {
        get; set;
    }
    public string Type { get; set; } = "Electricity";
    public string SerialNumber { get; set; } = "";
    public string Unit { get; set; } = "kWh";
    public decimal LastValue
    {
        get; set;
    }
    public bool Active { get; set; } = true;
    public uint Version
    {
        get; set;
    }
}

public sealed class Reading : Owned
{
    public Guid MeterId
    {
        get; set;
    }
    public decimal PreviousValue
    {
        get; set;
    }
    public decimal Value
    {
        get; set;
    }
    public Guid? PhotoId
    {
        get; set;
    }
    public string Status { get; set; } = "Submitted";
    public string? ReviewReason
    {
        get; set;
    }
}
