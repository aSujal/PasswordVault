using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Models;

public class Password
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }


    [BsonRef("categories")]
    public Category? Category { get; set; }

    public List<string> Tags { get; set; } = new List<string>();
    public bool IsFavorite { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    // Sync metadata
    public bool IsDeleted { get; set; } = false;
    public long SyncVersion { get; set; } = 0;
}

