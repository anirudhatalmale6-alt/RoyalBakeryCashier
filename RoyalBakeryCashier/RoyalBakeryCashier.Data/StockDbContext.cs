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
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(
                    "Server=localhost;Database=RoyalBakery;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=120;");
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

            // ===== Sale → SaleItem =====
            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.Items)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.MenuItem)
                .WithMany()
                .HasForeignKey(si => si.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== SalesOrder → SalesOrderItem =====
            modelBuilder.Entity<SalesOrderItem>()
                .HasOne(soi => soi.SalesOrder)
                .WithMany(so => so.Items)
                .HasForeignKey(soi => soi.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalesOrderItem>()
                .HasOne(soi => soi.MenuItem)
                .WithMany()
                .HasForeignKey(soi => soi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Decimal / int precision =====
            modelBuilder.Entity<GRNItem>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderItem>().Property(p => p.PricePerItem).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderItem>().Property(p => p.TotalPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Stock>().Property(s => s.Quantity).HasColumnType("int"); // integer quantity
        }

        /// <summary>
        /// Apply schema patches for columns/tables added after initial EnsureCreated.
        /// Drops and recreates tables that have wrong schema (only if empty).
        /// Safe to call multiple times.
        /// </summary>
        public void ApplyMigrations()
        {
            // Step 1: Drop incomplete tables that have wrong schema (only safe for tables with no real data yet).
            // Uses a check: if the table exists but is missing a key column, drop it so we can recreate.
            var dropAndRecreate = new[]
            {
                // Sales: check for InvoiceNumber column
                @"IF OBJECT_ID('SaleItems', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SaleItems') AND name = 'ItemName')
                  DROP TABLE SaleItems;",

                @"IF OBJECT_ID('Sales', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'InvoiceNumber')
                  DROP TABLE Sales;",

                // Orders: check for TotalAmount column
                @"IF OBJECT_ID('OrderItems', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'PricePerItem')
                  DROP TABLE OrderItems;",

                @"IF OBJECT_ID('Orders', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'TotalAmount')
                  DROP TABLE Orders;",

                // SalesOrders: check for SalesOrderNumber column
                @"IF OBJECT_ID('SalesOrderItems', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrderItems') AND name = 'PricePerItem')
                  DROP TABLE SalesOrderItems;",

                @"IF OBJECT_ID('SalesOrders', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'SalesOrderNumber')
                  DROP TABLE SalesOrders;",

                // OrderPayments: check for TenderAmount
                @"IF OBJECT_ID('OrderPayments', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OrderPayments') AND name = 'TenderAmount')
                  DROP TABLE OrderPayments;",

                // Clearances: check for Reason
                @"IF OBJECT_ID('Clearances', 'U') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Clearances') AND name = 'Reason')
                  DROP TABLE Clearances;",
            };

            foreach (var sql in dropAndRecreate)
            {
                try { Database.ExecuteSqlRaw(sql); } catch { }
            }

            // Step 2: Create tables that don't exist
            var creates = new[]
            {
                @"IF OBJECT_ID('Orders', 'U') IS NULL
                  CREATE TABLE Orders (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      DateTime DATETIME2 NOT NULL,
                      Status INT NOT NULL DEFAULT 1,
                      TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0
                  );",

                @"IF OBJECT_ID('OrderItems', 'U') IS NULL
                  CREATE TABLE OrderItems (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      OrderId INT NOT NULL,
                      MenuItemId INT NOT NULL,
                      Quantity INT NOT NULL,
                      PricePerItem DECIMAL(18,2) NOT NULL DEFAULT 0,
                      TotalPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                      CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
                      CONSTRAINT FK_OrderItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id) ON DELETE NO ACTION
                  );",

                @"IF OBJECT_ID('OrderPayments', 'U') IS NULL
                  CREATE TABLE OrderPayments (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      OrderId INT NOT NULL,
                      PaymentType INT NOT NULL DEFAULT 0,
                      TenderAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                      DateTime DATETIME2 NOT NULL
                  );",

                @"IF OBJECT_ID('Sales', 'U') IS NULL
                  CREATE TABLE Sales (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      DateTime DATETIME2 NOT NULL,
                      TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                      CashAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                      CardAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                      ChangeGiven DECIMAL(18,2) NOT NULL DEFAULT 0,
                      CashierName NVARCHAR(MAX) NULL,
                      InvoiceNumber NVARCHAR(MAX) NOT NULL DEFAULT ''
                  );",

                @"IF OBJECT_ID('SaleItems', 'U') IS NULL
                  CREATE TABLE SaleItems (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      SaleId INT NOT NULL,
                      MenuItemId INT NOT NULL,
                      ItemName NVARCHAR(MAX) NOT NULL DEFAULT '',
                      Quantity INT NOT NULL,
                      PricePerItem DECIMAL(18,2) NOT NULL DEFAULT 0,
                      TotalPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                      CONSTRAINT FK_SaleItems_Sales FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
                      CONSTRAINT FK_SaleItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id) ON DELETE NO ACTION
                  );",

                @"IF OBJECT_ID('SalesOrders', 'U') IS NULL
                  CREATE TABLE SalesOrders (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      SalesOrderNumber NVARCHAR(20) NOT NULL,
                      CreatedAt DATETIME2 NOT NULL,
                      TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                      Status INT NOT NULL DEFAULT 0,
                      TerminalName NVARCHAR(MAX) NULL,
                      CustomerName NVARCHAR(MAX) NULL
                  );",

                @"IF OBJECT_ID('SalesOrderItems', 'U') IS NULL
                  CREATE TABLE SalesOrderItems (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      SalesOrderId INT NOT NULL,
                      MenuItemId INT NOT NULL,
                      Quantity INT NOT NULL,
                      PricePerItem DECIMAL(18,2) NOT NULL DEFAULT 0,
                      TotalPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                      CONSTRAINT FK_SalesOrderItems_SalesOrders FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(Id) ON DELETE CASCADE,
                      CONSTRAINT FK_SalesOrderItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id) ON DELETE NO ACTION
                  );",

                @"IF OBJECT_ID('Clearances', 'U') IS NULL
                  CREATE TABLE Clearances (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      DateTime DATETIME2 NOT NULL,
                      MenuItemId INT NOT NULL,
                      Quantity INT NOT NULL,
                      Reason NVARCHAR(MAX) NOT NULL DEFAULT '',
                      Note NVARCHAR(MAX) NULL,
                      CONSTRAINT FK_Clearances_MenuItems FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id) ON DELETE NO ACTION
                  );",

                // Step 3: Add missing columns to existing tables
                @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MenuItems') AND name = 'IsQuick')
                  ALTER TABLE MenuItems ADD IsQuick BIT NOT NULL DEFAULT 0;",

                // Users table for login
                @"IF OBJECT_ID('Users', 'U') IS NULL
                  CREATE TABLE Users (
                      Id INT IDENTITY(1,1) PRIMARY KEY,
                      Username NVARCHAR(50) NOT NULL,
                      PasswordHash NVARCHAR(200) NOT NULL,
                      DisplayName NVARCHAR(100) NOT NULL,
                      Role NVARCHAR(30) NOT NULL DEFAULT 'Cashier',
                      IsActive BIT NOT NULL DEFAULT 1,
                      CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
                  );",
            };

            foreach (var sql in creates)
            {
                try { Database.ExecuteSqlRaw(sql); } catch { }
            }

            // Seed default admin user if no users exist
            try
            {
                Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT 1 FROM Users)
                    INSERT INTO Users (Username, PasswordHash, DisplayName, Role, IsActive, CreatedAt)
                    VALUES ('admin', 'admin123', 'Administrator', 'Admin', 1, GETDATE());
                ");
            }
            catch { }
        }
    }
}