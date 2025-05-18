using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasswordVault.Services.Sync;

public class SyncService
{
    private readonly DatabaseService _dbService;
    private readonly ICryptoService _cryptoService;

    public SyncService(DatabaseService dbService, ICryptoService cryptoService)
    {
        _dbService = dbService;
        _cryptoService = cryptoService;
    }

    public void StartListening()
    {
        // Create a simple UDP listener for device discovery
        Task.Run(() => ListenForDevices());
    }

    private async Task ListenForDevices()
    {
        var udpClient = new UdpClient(45678);

        try
        {
            while (true)
            {
                var result = await udpClient.ReceiveAsync();
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message.StartsWith("PWMGR:DISCOVER"))
                {
                    // Respond with device info
                    var deviceInfo = CreateDeviceInfo();
                    var response = Encoding.UTF8.GetBytes(deviceInfo);
                    await udpClient.SendAsync(response, response.Length, result.RemoteEndPoint);
                }
                else if (message.StartsWith("PWMGR:SYNC"))
                {
                    // Handle sync request
                    await HandleSyncRequest(result.RemoteEndPoint);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in listener: {ex.Message}");
        }
        finally
        {
            udpClient.Close();
        }
    }

    private string CreateDeviceInfo()
    {
        // Create a JSON string with device info for discovery
        var deviceInfo = new
        {
            Name = Environment.MachineName,
            Id = GetDeviceId(),
            Version = "1.0",
            LastSync = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(deviceInfo);
    }

    private string GetDeviceId()
    {
        // Get or create a unique device ID
        // Simple implementation for example
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordVault",
            "device_id.txt");

        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        var deviceId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, deviceId);

        return deviceId;
    }

    private async Task HandleSyncRequest(IPEndPoint remoteEndPoint)
    {
        // Simple implementation for handling sync
        // In a real app, this would involve secure key exchange and delta sync
        using (var tcpClient = new TcpClient())
        {
            try
            {
                await tcpClient.ConnectAsync(remoteEndPoint.Address, 45679);

                using (var stream = tcpClient.GetStream())
                {
                    // Exchange sync data
                    await SendChanges(stream);
                    await ReceiveChanges(stream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync error: {ex.Message}");
            }
        }
    }

    private async Task SendChanges(NetworkStream stream)
    {
        // Send changed passwords since last sync
        using (var db = _dbService.OpenDatabase())
        {
            var collection = db.GetCollection<Password>("passwords");
            var deviceCollection = db.GetCollection<SyncDevice>("syncDevices");

            var targetDevice = deviceCollection.FindAll().FirstOrDefault();
            if (targetDevice == null) return;

            var changes = collection.Find(p => p.SyncVersion > targetDevice.LastSyncVersion).ToList();

            // Serialize and encrypt changes
            var serializedChanges = JsonSerializer.Serialize(changes);
            var encryptedChanges = _cryptoService.Encrypt(serializedChanges);

            // Send data length first
            byte[] lengthBytes = BitConverter.GetBytes(encryptedChanges.Length);
            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);

            // Send encrypted data
            await stream.WriteAsync(encryptedChanges, 0, encryptedChanges.Length);
        }
    }

    private async Task ReceiveChanges(NetworkStream stream)
    {
        // Read length first
        byte[] lengthBytes = new byte[4];
        await stream.ReadAsync(lengthBytes, 0, lengthBytes.Length);
        int length = BitConverter.ToInt32(lengthBytes, 0);

        // Read encrypted data
        byte[] encryptedData = new byte[length];
        await stream.ReadAsync(encryptedData, 0, length);

        // Decrypt and process changes
        string decryptedData = _cryptoService.Decrypt(encryptedData);
        var changes = JsonSerializer.Deserialize<List<Password>>(decryptedData);

        // Apply changes to local database
        ApplyChanges(changes);
    }

    private void ApplyChanges(List<Password> changes)
    {
        using (var db = _dbService.OpenDatabase())
        {
            var collection = db.GetCollection<Password>("passwords");

            foreach (var password in changes)
            {
                var existing = collection.FindById(password.Id);

                if (existing == null || existing.SyncVersion < password.SyncVersion)
                {
                    collection.Upsert(password);
                }
            }
        }
    }
}