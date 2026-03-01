using System.ComponentModel.DataAnnotations;

namespace RoyalBakeryCashier.Data.Entities;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Role: "Cashier", "Salesman", "Admin", "GRN"
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = "Cashier";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
