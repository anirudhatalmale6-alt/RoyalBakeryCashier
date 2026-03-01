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

// Auto-create tables and seed default admin user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BakeryDbContext>();
    try
    {
        db.Database.EnsureCreated();

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
        Console.WriteLine($"DB init warning: {ex.Message}");
        // Try creating just the Users table if it doesn't exist
        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(50) NOT NULL,
                    PasswordHash NVARCHAR(200) NOT NULL,
                    DisplayName NVARCHAR(100) NOT NULL,
                    Role NVARCHAR(30) NOT NULL DEFAULT 'Cashier',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
                )");
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
                Console.WriteLine("Users table created + default admin user seeded");
            }
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"Users table creation failed: {ex2.Message}");
        }
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
