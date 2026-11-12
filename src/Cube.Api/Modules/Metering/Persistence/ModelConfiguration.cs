using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Modules.Metering.Persistence;

internal sealed class MeterConfiguration : IEntityTypeConfiguration<Meter>
{
    public void Configure(EntityTypeBuilder<Meter> entity)
    {
        entity.HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.Version).IsRowVersion();
        entity.HasIndex(x => x.SerialNumber).IsUnique();
        entity.ToTable(t => t.HasCheckConstraint("meter_baseline", "\"LastValue\" >= 0"));
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
    }
}

internal sealed class ReadingConfiguration : IEntityTypeConfiguration<Reading>
{
    public void Configure(EntityTypeBuilder<Reading> entity)
    {
        entity.HasOne<Meter>().WithMany().HasForeignKey(x => new { x.TenantId, x.MeterId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        entity.ToTable(t => t.HasCheckConstraint("reading_monotonic", "\"Value\" >= \"PreviousValue\""));
    }
}
