using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Modules.Billing.Persistence;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
        entity.HasIndex(x => x.ExternalId).IsUnique();
        entity.ToTable(t => t.HasCheckConstraint("invoice_amounts", "\"Amount\" > 0 AND \"Paid\" >= 0 AND \"Paid\" <= \"Amount\""));
    }
}

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> entity)
    {
        entity.HasIndex(x => new { x.Provider, x.TransactionId }).IsUnique();
        entity.HasOne<Invoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.InvoiceId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

