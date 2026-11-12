using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public sealed class PaymentRecord : Owned { public Guid InvoiceId { get; set; } public string Provider { get; set; } = ""; public string TransactionId { get; set; } = ""; public decimal Amount { get; set; } public string Currency { get; set; } = ""; }
// Only a future authenticated provider adapter should call this after verifying settlement.
// No public endpoint accepts payment success or accounting amounts from a tenant.
public sealed class PaymentLedger(CubeDb db)
{
    public async Task<Guid> ApplyVerifiedAsync(Guid invoiceId, string provider, string transactionId, decimal amount, string currency, CancellationToken ct)
    {
        Guard.Text(provider, "provider", 100); Guard.Text(transactionId, "transaction", 200); Guard.That(amount > 0 && decimal.Round(amount, 2) == amount, "invalid_amount");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var invoice = await db.Invoices.FromSqlInterpolated($"SELECT * FROM \"Invoices\" WHERE \"Id\" = {invoiceId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new ApiError(404, "invoice_not_found");
        await db.Entry(invoice).ReloadAsync(ct);
        var existing = await db.Set<PaymentRecord>().SingleOrDefaultAsync(x => x.Provider == provider && x.TransactionId == transactionId, ct);
        if (existing is not null) { Guard.That(existing.InvoiceId == invoiceId && existing.Amount == amount && existing.Currency == currency, "payment_replay_mismatch", 409); return existing.Id; }
        Guard.That(invoice.Currency == currency && amount <= invoice.Amount - invoice.Paid, "payment_amount_or_currency_mismatch", 409);
        var payment = new PaymentRecord { TenantId = invoice.TenantId, InvoiceId = invoiceId, Provider = provider, TransactionId = transactionId, Amount = amount, Currency = currency }; db.Add(payment); invoice.Paid += amount;
        Events.Queue(db, "Accounting", "PaymentSettled", payment.Id); await Events.NotifyAsync(db, invoice.TenantId, "PaymentSettled", invoiceId, "A payment has been recorded", ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return payment.Id;
    }
}
