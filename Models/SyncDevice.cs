using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;

namespace PasswordVault.Models;

public class SyncDevice
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Device information
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty; // Unique hardword identifier

    // For device-to-device encryption
    public byte[] PublicKey { get; set; } = [];

    // Sync metadata
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public long LastSyncVersion { get; set; } = 0;
}
