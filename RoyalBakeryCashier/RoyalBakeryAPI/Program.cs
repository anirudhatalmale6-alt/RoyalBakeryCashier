using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddDbContext<BakeryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=.\\SQLEXPRESS;Database=RoyalBakery;Trusted_Connection=True;TrustServerCertificate=True;"));

// Allow all origins (for local network Android access)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Auto-create any missing tables and seed default admin user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BakeryDbContext>();
    try
    {
        // Create all missing tables with IF NOT EXISTS checks
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MenuCategories' AND xtype='U')
            CREATE TABLE MenuCategories (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MenuItems' AND xtype='U')
            CREATE TABLE MenuItems (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(MAX) NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                MenuCategoryId INT NOT NULL,
                IsQuick BIT NOT NULL DEFAULT 0
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Stocks' AND xtype='U')
            CREATE TABLE Stocks (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                MenuItemId INT NOT NULL,
                Quantity INT NOT NULL,
                FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id)
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GRNs' AND xtype='U')
            CREATE TABLE GRNs (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                GRNNumber NVARCHAR(MAX) NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GRNItems' AND xtype='U')
            CREATE TABLE GRNItems (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                GRNId INT NOT NULL,
                MenuItemId INT NOT NULL,
                Quantity INT NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                CurrentQuantity INT NOT NULL,
                FOREIGN KEY (GRNId) REFERENCES GRNs(Id),
                FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id)
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GRNAdjustmentRequests' AND xtype='U')
            CREATE TABLE GRNAdjustmentRequests (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                GRNId INT NOT NULL,
                Reason NVARCHAR(MAX) NOT NULL,
                AdminCode NVARCHAR(MAX) NOT NULL,
                IsApproved BIT NOT NULL DEFAULT 0,
                RequestedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                FOREIGN KEY (GRNId) REFERENCES GRNs(Id)
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GRNAdjustmentRequestItems' AND xtype='U')
            CREATE TABLE GRNAdjustmentRequestItems (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                GRNAdjustmentRequestId INT NOT NULL,
                MenuItemId INT NOT NULL,
                ItemName NVARCHAR(MAX) NOT NULL,
                RequestedQuantity INT NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                FOREIGN KEY (GRNAdjustmentRequestId) REFERENCES GRNAdjustmentRequests(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Clearances' AND xtype='U')
            CREATE TABLE Clearances (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                DateTime DATETIME2 NOT NULL DEFAULT GETDATE(),
                MenuItemId INT NOT NULL,
                Quantity INT NOT NULL,
                Reason NVARCHAR(MAX) NOT NULL,
                Note NVARCHAR(MAX) NULL,
                FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id)
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
            CREATE TABLE Users (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Username NVARCHAR(50) NOT NULL,
                PasswordHash NVARCHAR(200) NOT NULL,
                DisplayName NVARCHAR(100) NOT NULL,
                Role NVARCHAR(30) NOT NULL DEFAULT 'Cashier',
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        ");
        Console.WriteLine("Database tables verified/created.");

        // Seed default admin user if no users exist
        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = "admin123",
                DisplayName = "Admin",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            db.SaveChanges();
            Console.WriteLine("Default admin user created (admin/admin123)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB init error: {ex.Message}");
    }
}

app.UseCors();
app.MapControllers();

// Listen on all interfaces so Android devices can reach it
app.Urls.Add("http://0.0.0.0:5000");

Console.WriteLine("Royal Bakery API running on http://0.0.0.0:5000");
Console.WriteLine("Endpoints:");
Console.WriteLine("  GET  /api/menu/categories");
Console.WriteLine("  GET  /api/menu/items");
Console.WriteLine("  GET  /api/stock");
Console.WriteLine("  GET  /api/stock/{menuItemId}");
Console.WriteLine("  GET  /api/grn");
Console.WriteLine("  GET  /api/grn/{id}");
Console.WriteLine("  POST /api/grn");
Console.WriteLine("  GET  /api/adjustment");
Console.WriteLine("  POST /api/adjustment");
Console.WriteLine("  POST /api/adjustment/{id}/approve");
Console.WriteLine("  GET  /api/clearance");
Console.WriteLine("  POST /api/clearance");

app.Run();
