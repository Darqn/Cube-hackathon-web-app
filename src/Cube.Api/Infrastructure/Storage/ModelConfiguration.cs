using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Infrastructure.Storage;

internal sealed class PublishedAssetConfiguration : IEntityTypeConfiguration<PublishedAsset>
{
    public void Configure(EntityTypeBuilder<PublishedAsset> entity)
    {
        entity.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<News>().WithMany().HasForeignKey(x => x.NewsId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(t => t.HasCheckConstraint("one_asset_parent", "(\"ListingId\" IS NULL) <> (\"NewsId\" IS NULL)"));
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> entity)
    {
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
    }
}

