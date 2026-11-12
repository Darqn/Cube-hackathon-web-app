using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Modules.Documents.Persistence;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> entity)
    {
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.FileId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        entity.HasOne<Lease>().WithMany().HasForeignKey(x => new { x.TenantId, x.LeaseId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

