namespace Cube.Api;

public sealed class Invoice : Owned
{
    public string ExternalId { get; set; } = "";
    public string Number { get; set; } = "";
    public string Kind { get; set; } = "Rent";
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public string Purpose { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public string Currency { get; set; } = "RUB";
    public Guid? FileId { get; set; }
}
