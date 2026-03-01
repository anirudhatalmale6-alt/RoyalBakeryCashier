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
