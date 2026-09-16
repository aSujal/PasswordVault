using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;

namespace PasswordVault.Models;

// File bytes live in LiteDB FileStorage under StorageId
public partial class VaultDocument : ObservableObject
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? PasswordId { get; set; }
    public Guid? FolderId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    // LiteDB FileStorage id for this documents bytes, e.g. "doc/{Id}".
    public string StorageId { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; } = false;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // UI only
    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private bool _isSelected;
}
