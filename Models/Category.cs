using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Models;
public class Category
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4285F4";  // blue
    public string Icon { get; set; } = "Key";

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Sync metadata
    public bool IsDeleted { get; set; } = false;
    public long SyncVersion { get; set; } = 0;
}
