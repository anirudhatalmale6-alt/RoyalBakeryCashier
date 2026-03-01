using Microsoft.EntityFrameworkCore;

namespace RoyalBakeryAPI.Models;

public class BakeryDbContext : DbContext
{
    public BakeryDbContext(DbContextOptions<BakeryDbContext> options) : base(options) { }

    public DbSet<MenuCategory> MenuCategories { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<GRN> GRNs { get; set; }
    public DbSet<GRNItem> GRNItems { get; set; }
    public DbSet<GRNAdjustmentRequest> GRNAdjustmentRequests { get; set; }
    public DbSet<GRNAdjustmentRequestItem> GRNAdjustmentRequestItems { get; set; }
    public DbSet<Clearance> Clearances { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GRNItem>()
            .HasOne(g => g.GRN)
            .WithMany(grn => grn.Items)
            .HasForeignKey(g => g.GRNId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GRNItem>()
            .HasOne(g => g.MenuItem)
            .WithMany()
            .HasForeignKey(g => g.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Stock>()
            .HasOne(s => s.MenuItem)
            .WithMany()
            .HasForeignKey(s => s.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GRNAdjustmentRequest>()
            .HasOne(r => r.GRN)
            .WithMany()
            .HasForeignKey(r => r.GRNId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GRNAdjustmentRequestItem>()
            .HasOne(i => i.GRNAdjustmentRequest)
            .WithMany(r => r.RequestedItems)
            .HasForeignKey(i => i.GRNAdjustmentRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Clearance>()
            .HasOne(c => c.MenuItem)
            .WithMany()
            .HasForeignKey(c => c.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GRNItem>().Property(p => p.Price).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Stock>().Property(s => s.Quantity).HasColumnType("int");
    }
}
