using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

namespace RoyalBakeryAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BakeryDbContext _db;

    public AuthController(BakeryDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Login with username and password.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required." });

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null || user.PasswordHash != request.Password)
            return Unauthorized(new { message = "Invalid username or password." });

        return Ok(new LoginResponse
        {
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role
        });
    }
}
