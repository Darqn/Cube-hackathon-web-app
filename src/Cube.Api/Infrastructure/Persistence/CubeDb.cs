using Cube.Api.Common.Identity;
using Cube.Api.Infrastructure.Messaging;
using Cube.Api.Infrastructure.Storage;
using Cube.Api.Modules.Billing.Persistence;
using Cube.Api.Modules.Documents.Persistence;
using Cube.Api.Modules.Identity.Persistence;
using Cube.Api.Modules.Metering.Persistence;
using Cube.Api.Modules.News.Persistence;
using Cube.Api.Modules.Notifications.Persistence;
using Cube.Api.Modules.Property.Persistence;
using Cube.Api.Modules.Requests.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public sealed class CubeDb(DbContextOptions<CubeDb> options, ICurrentActor actor) : IdentityDbContext<User, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Premises> Premises => Set<Premises>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();
    public DbSet<RequestMessage> Messages => Set<RequestMessage>();
    public DbSet<RequestEvent> RequestEvents => Set<RequestEvent>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingInquiry> Inquiries => Set<ListingInquiry>();
    public DbSet<News> News => Set<News>();
    public DbSet<NewsReceipt> Receipts => Set<NewsReceipt>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Outbox> Outbox => Set<Outbox>();
    public DbSet<Audit> Audit => Set<Audit>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<PaymentRecord>();
        b.Entity<PublishedAsset>();
        foreach (var entity in b.Model.GetEntityTypes().ToList())
        {
            if (typeof(Owned).IsAssignableFrom(entity.ClrType))
            {
                b.Entity(entity.ClrType).HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
                b.Entity(entity.ClrType).HasIndex("TenantId", "CreatedAt");
            }
            foreach (var p in entity.GetProperties())
            {
                if (p.ClrType == typeof(decimal))
                {
                    p.SetPrecision(18);
                    p.SetScale(4);
                }
                if (p.ClrType == typeof(string) && p.GetMaxLength() is null)
                    p.SetMaxLength(10000);
            }
        }
        b.ApplyConfiguration(new TenantConfiguration());
        b.ApplyConfiguration(new PremisesConfiguration());
        b.ApplyConfiguration(new LeaseConfiguration());
        b.ApplyConfiguration(new ListingConfiguration());
        b.ApplyConfiguration(new ListingInquiryConfiguration());
        b.ApplyConfiguration(new UserConfiguration());
        b.ApplyConfiguration(new SessionConfiguration());
        b.ApplyConfiguration(new InvoiceConfiguration());
        b.ApplyConfiguration(new PaymentRecordConfiguration());
        b.ApplyConfiguration(new CategoryConfiguration());
        b.ApplyConfiguration(new ServiceRequestConfiguration());
        b.ApplyConfiguration(new RequestMessageConfiguration());
        b.ApplyConfiguration(new RequestEventConfiguration());
        b.ApplyConfiguration(new AttachmentConfiguration());
        b.ApplyConfiguration(new MeterConfiguration());
        b.ApplyConfiguration(new ReadingConfiguration());
        b.ApplyConfiguration(new DocumentConfiguration());
        b.ApplyConfiguration(new NewsReceiptConfiguration());
        b.ApplyConfiguration(new NotificationConfiguration());
        b.ApplyConfiguration(new PublishedAssetConfiguration());
        b.ApplyConfiguration(new StoredFileConfiguration());
        b.ApplyConfiguration(new OutboxConfiguration());
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (actor.UserId is Guid actorId)
        {
            var entries = ChangeTracker.Entries<Entity>().Where(x => x.Entity is not Cube.Api.Audit and not Session && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            foreach (var entry in entries)
                Audit.Add(new Audit { ActorId = actorId, Action = $"{entry.State}:{entry.Entity.GetType().Name}", ResourceId = entry.Entity.Id });
        }
        return base.SaveChangesAsync(ct);
    }
}
