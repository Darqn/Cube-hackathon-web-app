using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Modules.News.Persistence;

internal sealed class NewsReceiptConfiguration : IEntityTypeConfiguration<NewsReceipt>
{
    public void Configure(EntityTypeBuilder<NewsReceipt> entity)
    {
        entity.HasIndex(x => new { x.NewsId, x.UserId }).IsUnique();
        entity.HasOne<global::Cube.Api.News>().WithMany().HasForeignKey(x => x.NewsId).OnDelete(DeleteBehavior.Restrict);
    }
}
