using Microsoft.EntityFrameworkCore;
using RoyalBakeryCashier.Data.Entities;

namespace RoyalBakeryCashier.Data
{
    public class StockDbContext : DbContext
    {
        // Runtime DI constructor
        public StockDbContext(DbContextOptions<StockDbContext> options) : base(options) { }

        // Parameterless constructor for design-time migrations
        public StockDbContext() { }

        // DbSets
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; } // plural
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<OrderPayments> OrderPayments { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<GRN> GRNs { get; set; }
        public DbSet<GRNItem> GRNItems { get; set; }
        public DbSet<Clearance> Clearances { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(
                    "Server=.\\SQLEXPRESS;Database=RoyalBakery;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=120;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== Order → OrderItem =====
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== OrderItem → MenuItem =====
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.MenuItem)
                .WithMany()
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Stock → MenuItem =====
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.MenuItem)
                .WithMany()
                .HasForeignKey(s => s.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== GRNItem → MenuItem =====
            modelBuilder.Entity<GRNItem>()
                .HasOne(g => g.MenuItem)
                .WithMany()
                .HasForeignKey(g => g.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== GRNItem → GRN (FIXED) =====
            modelBuilder.Entity<GRNItem>()
                .HasOne(g => g.GRN)
                .WithMany(grn => grn.Items) // GRN must have: public ICollection<GRNItem> Items { get; set; }
                .HasForeignKey(g => g.GRNId)
                .HasConstraintName("FK_GRNItem_GRN")
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Decimal / int precision =====
            modelBuilder.Entity<GRNItem>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderItem>().Property(p => p.PricePerItem).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderItem>().Property(p => p.TotalPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Stock>().Property(s => s.Quantity).HasColumnType("int"); // integer quantity
        }
    }
}