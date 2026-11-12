using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Modules.Property.Persistence;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.HasIndex(x => x.Inn).IsUnique();
    }
}

internal sealed class PremisesConfiguration : IEntityTypeConfiguration<Premises>
{
    public void Configure(EntityTypeBuilder<Premises> entity)
    {
        entity.ToTable(t => { t.HasCheckConstraint("positive_area", "\"Area\" > 0"); t.HasCheckConstraint("positive_rent", "\"MonthlyRent\" >= 0"); });
    }
}

internal sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> entity)
    {
        entity.HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.PremisesId).IsUnique().HasFilter("\"Status\" = 'Active'");
        entity.ToTable(t => t.HasCheckConstraint("lease_dates", "\"StartsOn\" <= \"EndsOn\""));
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
    }
}

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> entity)
    {
        entity.HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ListingInquiryConfiguration : IEntityTypeConfiguration<ListingInquiry>
{
    public void Configure(EntityTypeBuilder<ListingInquiry> entity)
    {
        entity.HasIndex(x => new { x.TenantId, x.ListingId }).IsUnique();
        entity.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}
