using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public sealed class CubeDb(DbContextOptions<CubeDb> options, IHttpContextAccessor http) : IdentityDbContext<User, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>(); public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Premises> Premises => Set<Premises>(); public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<Invoice> Invoices => Set<Invoice>(); public DbSet<Category> Categories => Set<Category>();
    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>(); public DbSet<RequestMessage> Messages => Set<RequestMessage>();
    public DbSet<RequestEvent> RequestEvents => Set<RequestEvent>(); public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<Reading> Readings => Set<Reading>(); public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<Document> Documents => Set<Document>(); public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Listing> Listings => Set<Listing>(); public DbSet<ListingInquiry> Inquiries => Set<ListingInquiry>();
    public DbSet<News> News => Set<News>(); public DbSet<NewsReceipt> Receipts => Set<NewsReceipt>();
    public DbSet<Notification> Notifications => Set<Notification>(); public DbSet<Outbox> Outbox => Set<Outbox>();
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
                if (p.ClrType == typeof(decimal)) { p.SetPrecision(18); p.SetScale(4); }
                if (p.ClrType == typeof(string) && p.GetMaxLength() is null) p.SetMaxLength(10000);
            }
        }
        b.Entity<PublishedAsset>().HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PublishedAsset>().HasOne<News>().WithMany().HasForeignKey(x => x.NewsId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PublishedAsset>().ToTable(t => t.HasCheckConstraint("one_asset_parent", "(\"ListingId\" IS NULL) <> (\"NewsId\" IS NULL)"));
        b.Entity<Tenant>().HasIndex(x => x.Inn).IsUnique();
        b.Entity<User>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Session>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<Session>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Premises>().ToTable(t => { t.HasCheckConstraint("positive_area", "\"Area\" > 0"); t.HasCheckConstraint("positive_rent", "\"MonthlyRent\" >= 0"); });
        b.Entity<Lease>().HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Lease>().HasIndex(x => x.PremisesId).IsUnique().HasFilter("\"Status\" = 'Active'");
        b.Entity<Lease>().ToTable(t => t.HasCheckConstraint("lease_dates", "\"StartsOn\" <= \"EndsOn\""));
        b.Entity<PaymentRecord>().HasIndex(x => new { x.Provider, x.TransactionId }).IsUnique();
        b.Entity<PaymentRecord>().HasOne<Invoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.InvoiceId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Invoice>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Invoice>().HasIndex(x => x.ExternalId).IsUnique();
        b.Entity<Invoice>().ToTable(t => t.HasCheckConstraint("invoice_amounts", "\"Amount\" > 0 AND \"Paid\" >= 0 AND \"Paid\" <= \"Amount\""));
        b.Entity<Category>().HasIndex(x => x.Code).IsUnique();
        b.Entity<ServiceRequest>().HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ServiceRequest>().HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ServiceRequest>().HasOne<User>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ServiceRequest>().Property(x => x.Version).IsRowVersion();
        b.Entity<ServiceRequest>().HasIndex(x => new { x.AssigneeId, x.Status });
        b.Entity<Meter>().HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Meter>().Property(x => x.Version).IsRowVersion();
        b.Entity<Meter>().HasIndex(x => x.SerialNumber).IsUnique();
        b.Entity<Meter>().ToTable(t => t.HasCheckConstraint("meter_baseline", "\"LastValue\" >= 0"));
        // Composite keys prevent accidental cross-tenant child references, even outside HTTP code.
        b.Entity<ServiceRequest>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<RequestMessage>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<RequestMessage>().HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<RequestEvent>().HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Meter>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Reading>().HasOne<Meter>().WithMany().HasForeignKey(x => new { x.TenantId, x.MeterId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Reading>().ToTable(t => t.HasCheckConstraint("reading_monotonic", "\"Value\" >= \"PreviousValue\""));
        b.Entity<StoredFile>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Document>().HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.FileId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Attachment>().HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.FileId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Attachment>().HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Lease>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Document>().HasOne<Lease>().WithMany().HasForeignKey(x => new { x.TenantId, x.LeaseId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<Listing>().HasOne<Premises>().WithMany().HasForeignKey(x => x.PremisesId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ListingInquiry>().HasIndex(x => new { x.TenantId, x.ListingId }).IsUnique();
        b.Entity<ListingInquiry>().HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ListingInquiry>().HasOne<ServiceRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequestId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Entity<NewsReceipt>().HasIndex(x => new { x.NewsId, x.UserId }).IsUnique();
        b.Entity<NewsReceipt>().HasOne<News>().WithMany().HasForeignKey(x => x.NewsId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Outbox>().HasIndex(x => new { x.Provider, x.Kind, x.ResourceId }).IsUnique();
        b.Entity<Outbox>().HasIndex(x => new { x.Status, x.CreatedAt });
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.DeduplicationKey }).IsUnique();
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var actor = http.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(actor, out var actorId))
        {
            var entries = ChangeTracker.Entries<Entity>().Where(x => x.Entity is not Cube.Api.Audit and not Session && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            foreach (var entry in entries) Audit.Add(new Audit { ActorId = actorId, Action = $"{entry.State}:{entry.Entity.GetType().Name}", ResourceId = entry.Entity.Id });
        }
        return base.SaveChangesAsync(ct);
    }
}
