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

    // TOTP parameters for TwoFactorSecret. Most services use the defaults (SHA1/6 digits/30s),
    // captured when the secret is entered as a full otpauth:// URI instead of a bare key.
    // most but some use SHA256/SHA512 or a different digit/period count
    public string TwoFactorAlgorithm { get; set; } = Services.Totp.TotpService.DefaultAlgorithm;
    public int TwoFactorDigits { get; set; } = Services.Totp.TotpService.DefaultDigits;
    public int TwoFactorPeriod { get; set; } = Services.Totp.TotpService.DefaultPeriod;

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

    // Current TOTP code and countdown, refreshed every second by PasswordListViewModel while
    // LiveTwoFactorSecret is set - lets the list show a live code instead of only on click.
    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private string? _currentTotpCode;

    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private int _totpSecondsRemaining;

    // Fraction (0..1) of the current TOTP period remaining, drives the countdown ring in the list.
    [BsonIgnore]
    [ObservableProperty]
    [property: BsonIgnore]
    private double _totpProgressFraction;
}

