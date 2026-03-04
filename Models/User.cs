using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Models;

public class User
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Username { get; set; } = string.Empty;
    // Password hash (Argon2)
    public string PasswordHash { get; set; } = string.Empty;
    // Salt for password hashing
    public byte[] PasswordSalt { get; set; } = [];

    public string ThemeName { get; set; } = "System";
    public bool BiometricUnlockEnabled { get; set; } = false;
    public TimeSpan AutoLockTimeMinutes { get; set; } = TimeSpan.FromMinutes(5);


    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
}