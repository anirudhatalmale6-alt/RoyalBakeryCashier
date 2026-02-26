using Microsoft.EntityFrameworkCore;

namespace RoyalBakeryCashier.Data
{
    public class StockDbContext : DbContext
    {
        // Runtime DI constructor
        public StockDbContext(DbContextOptions<StockDbContext> options)
            : base(options) { }

        // Parameterless constructor for design-time migrations
        public StockDbContext() { }

        // DbSets for your entities
        public DbSet<Entities.Order> Orders { get; set; }
        public DbSet<Entities.OrderItem> OrderItem { get; set; }
        public DbSet<Entities.Stock> Stocks { get; set; }
        public DbSet<Entities.OrderPayments> OrderPayments { get; set; }
        public DbSet<Entities.MenuItem> MenuItems { get; set; }      // Use alias here
        public DbSet<Entities.MenuCategory> MenuCategories { get; set; }

        //public DbSet<Entities.GRNAdjustmentRequest> GRNAdjustmentRequest { get; set; }

        //public DbSet<Entities.GRNAdjustmentRequestItem> GRNAdjustmentRequestItem { get; set; }

        public DbSet<Entities.Clearance> Clearances { get; set; }
        // Uncomment when needed
        public DbSet<Entities.GRN> GRNs { get; set; }
         public DbSet<Entities.GRNItem> GRNItem { get; set; }
        // public DbSet<Entities.GRNAdjustmentRequest> GRNAdjustmentRequest { get; set; }
        // public DbSet<Entities.GRNAdjustmentRequestItem> GRNAdjustmentRequestItem { get; set; }
         //public DbSet<Entities.Clearance> Clearances { get; set; }

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

            // Example: Set decimal precision for GRNItem.Price
            // modelBuilder.Entity<Entities.GRNItem>()
            //     .Property(p => p.Price)
            //     .HasColumnType("decimal(18,2)");

            // Example: Set decimal precision for Stock.Quantity
            // modelBuilder.Entity<Entities.Stock>()
            //     .Property(s => s.Quantity)
            //     .HasColumnType("decimal(18,2)");

            // Optional: Map MENUITEM to existing table
            // modelBuilder.Entity<Entities.MenuItem>()
            //     .ToTable("MenuItems");
        }
    }
}
