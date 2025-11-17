using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ErpDbContext : DbContext {
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options) {}

    public DbSet<AppliedEvent> AppliedEvents => Set<AppliedEvent>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    // public DbSet<ProductionClose> ProductionCloses => Set<ProductionClose>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<OpApplied> OpApplied => Set<OpApplied>();
    public DbSet<Lease> Leases => Set<Lease>();


    protected override void OnModelCreating(ModelBuilder b) {
        base.OnModelCreating(b);

        b.Entity<AppliedEvent>(e =>
        {
            e.HasKey(x => x.Id);                 // Guid eventId
            e.Property(x => x.SeenAtUtc).IsRequired();
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);                 // Guid
            e.Property(x => x.TenantId).HasMaxLength(32).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(64).IsRequired();
            e.Property(x => x.Action).HasMaxLength(32).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            e.Property(x => x.CreatedUtc).IsRequired();
            // SentUtc/AckedUtc mogen null zijn
            e.HasIndex(x => new { x.AckedUtc, x.CreatedUtc }); 
        });

        b.Entity<SalesOrder>().Property(p => p.RowVersion).IsRowVersion();
        b.Entity<CustomerNote>().Property(p => p.RowVersion).IsRowVersion();
        b.Entity<StockMovement>().Property(p => p.RowVersion).IsRowVersion();
        // b.Entity<ProductionClose>().Property(p => p.RowVersion).IsRowVersion();
        b.Entity<OpApplied>().HasKey(x => x.OpId);
        b.Entity<Lease>().HasKey(x => x.TenantId);
        b.Entity<SalesOrder>().Property(p => p.Total).HasColumnType("decimal(18,2)");
        b.Entity<StockMovement>().Property(p => p.Qty).HasColumnType("decimal(18,3)");
    }
}
