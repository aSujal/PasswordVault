using System;

namespace PasswordVault.Models;

public class DocumentAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PasswordId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public byte[] Data { get; set; } = [];
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
