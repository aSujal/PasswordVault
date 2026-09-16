using System;
using LiteDB;

namespace PasswordVault.Models;

public class DocumentFolder
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4285F4";
    public string Icon { get; set; } = "fa-solid fa-folder";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
