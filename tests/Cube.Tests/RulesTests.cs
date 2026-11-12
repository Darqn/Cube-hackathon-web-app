using Cube.Api;
using Cube.Api.Modules.Billing.Domain;
using Cube.Api.Modules.Identity.Domain;
using Cube.Api.Modules.Requests.Domain;
using Xunit;
namespace Cube.Tests;

public sealed class RulesTests
{
    [Theory]
    [InlineData("New", "Accepted", true)]
    [InlineData("InProgress", "Completed", true)]
    [InlineData("New", "Closed", false)]
    [InlineData("Closed", "InProgress", false)]
    [InlineData("Completed", "InProgress", true)]
    [InlineData("Rejected", "Accepted", false)]
    public void Status_machine_rejects_invalid_transitions(string from, string to, bool expected) => Assert.Equal(expected, RequestRules.CanTransition(from, to));
    [Theory]
    [InlineData("7707083893", true)]
    [InlineData("500100732259", true)]
    [InlineData("0000000000", false)]
    [InlineData("7707083894", false)]
    [InlineData("abc", false)]
    public void Inn_checksum(string inn, bool expected) => Assert.Equal(expected, InnRules.ValidInn(inn));
    [Fact]
    public void Overdue_partial_invoice_keeps_financial_balance()
    {
        var invoice = new Invoice { Amount = 100, Paid = 25, DueOn = new DateOnly(2026, 1, 1) };
        Assert.Equal("Overdue", InvoiceRules.InvoiceStatus(invoice, new DateOnly(2026, 1, 2)));
        invoice.Paid = 100;
        Assert.Equal("Paid", InvoiceRules.InvoiceStatus(invoice, new DateOnly(2026, 1, 2)));
    }
    [Fact]
    public void File_signature_must_match_extension()
    {
        Assert.Throws<ApiError>(() => PrivateStorage.Validate("executable"u8.ToArray(), "invoice.pdf"));
        Assert.Throws<ApiError>(() => PrivateStorage.Validate("%PDF-test"u8.ToArray(), "file.exe"));
    }
    [Fact] public void Corrupt_office_archive_is_rejected() => Assert.Throws<ApiError>(() => PrivateStorage.Validate([80, 75, 3, 4, 0], "invoice.docx"));
}
