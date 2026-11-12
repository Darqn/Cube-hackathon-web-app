namespace Cube.Api.Modules.Billing.Domain;

public static class InvoiceRules
{
    public static string InvoiceStatus(Invoice i, DateOnly today) => i.Paid >= i.Amount ? "Paid" : i.DueOn < today ? "Overdue" : i.Paid > 0 ? "PartiallyPaid" : "Unpaid";
}
