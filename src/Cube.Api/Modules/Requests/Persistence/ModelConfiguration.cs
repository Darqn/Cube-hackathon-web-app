using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cube.Api.Modules.Requests.Persistence;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> entity)
    {
        entity.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> entity)
    {
        entity.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.Version).IsRowVersion();
        entity.HasIndex(x => new { x.AssigneeId, x.Status });
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
    }
}

internal sealed class RequestMessageConfiguration : IEntityTypeConfiguration<RequestMessage>
{
    public void Configure(EntityTypeBuilder<RequestMessage> entity)
    {
        entity.HasAlternateKey(x => new { x.TenantId, x.Id });
        entity.HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

internal sealed class RequestEventConfiguration : IEntityTypeConfiguration<RequestEvent>
{
    public void Configure(EntityTypeBuilder<RequestEvent> entity)
    {
        entity.HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> entity)
    {
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.FileId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        entity.HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

