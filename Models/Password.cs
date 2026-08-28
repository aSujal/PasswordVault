using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordVault.Models;

public partial class Password : ObservableObject
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? TwoFactorSecret { get; set; }


    [BsonRef("categories")]
    public Category? Category { get; set; }

    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    // Sync metadata
    public bool IsDeleted { get; set; } = false;
    public long SyncVersion { get; set; } = 0;

    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private string _strengthColor = "Transparent";

    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private string _strengthText = string.Empty;

    [BsonIgnore]
    public bool IsWeak => StrengthText == "Weak" || StrengthText == "Very Weak";

    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private bool _isSelected;

    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private string? _liveTwoFactorSecret;
}

