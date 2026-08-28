using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Models;

public enum BackupFrequency
{
    Manual = 0,
    OnLogin = 1,
    Daily = 2,
    Weekly = 3
}

public class User
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public byte[] PasswordSalt { get; set; } = [];

    public string ThemeName { get; set; } = "System";
    public bool BiometricUnlockEnabled { get; set; } = false;
    public TimeSpan AutoLockTimeMinutes { get; set; } = TimeSpan.FromMinutes(5);

    public bool AutoBackupEnabled { get; set; } = true;
    public BackupFrequency BackupFrequency { get; set; } = BackupFrequency.OnLogin;
    public string? BackupLocation { get; set; }
    public int BackupRetentionCount { get; set; } = 7;
    public DateTime? LastBackupAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
}