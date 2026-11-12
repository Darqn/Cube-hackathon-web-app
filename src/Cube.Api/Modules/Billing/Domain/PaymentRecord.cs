namespace Cube.Api;

public sealed class PaymentRecord : Owned
{
    public Guid InvoiceId
    {
        get; set;
    }
    public string Provider { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public decimal Amount
    {
        get; set;
    }
    public string Currency { get; set; } = "";
}
