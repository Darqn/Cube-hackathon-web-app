using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Infrastructure.Messaging;

internal sealed class OutboxConfiguration : IEntityTypeConfiguration<Outbox>
{
    public void Configure(EntityTypeBuilder<Outbox> entity)
    {
        entity.HasIndex(x => new { x.Provider, x.Kind, x.ResourceId }).IsUnique();
        entity.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

