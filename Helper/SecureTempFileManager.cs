using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using PasswordVault.Services.Auth;

namespace PasswordVault.Helper;

// Backs "Open externally" for documents: writes a decrypted copy to a per-run temp folder so
// the OS's default handler can open it, then shreds (zero-overwrite + delete) every path it
// handed out as soon as the vault locks or the app exits, plus a startup sweep in case the
// previous run never got the chance to. This is the one place in the app where document
// plaintext ever touches disk.
public class SecureTempFileManager
{
    private readonly string _tempRoot;
    private readonly List<string> _issuedPaths = [];
    private readonly object _lock = new();

    public SecureTempFileManager(IAuthService authService)
    {
        _tempRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordManager", "temp");

        authService.Locked += (_, _) => ShredAll();
    }

    // Deletes anything left over from a previous run that never shut down cleanly (crash, kill).
    public void SweepStaleFiles()
    {
        if (!Directory.Exists(_tempRoot)) return;
        try
        {
            foreach (var dir in Directory.GetDirectories(_tempRoot))
                ShredDirectory(dir);
        }
        catch { /* best effort */ }
    }

    // Writes content to a fresh temp file and launches the OS's default handler for it. Returns
    // the path so the caller can, e.g., log it - the manager itself owns cleanup.
    public async Task<string> OpenWithDefaultAppAsync(string fileName, Stream content)
    {
        var dir = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);

        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file);
        }
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Temporary);

        lock (_lock) { _issuedPaths.Add(path); }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return path;
    }

    public void ShredAll()
    {
        List<string> paths;
        lock (_lock)
        {
            paths = [.. _issuedPaths];
            _issuedPaths.Clear();
        }

        foreach (var path in paths)
            ShredFile(path);
    }

    private static void ShredDirectory(string dir)
    {
        try
        {
            foreach (var file in Directory.GetFiles(dir))
                ShredFile(file);
            Directory.Delete(dir, recursive: true);
        }
        catch { /* best effort - file may still be open in the external app */ }
    }

    private static void ShredFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var length = new FileInfo(path).Length;
                using (var stream = File.OpenWrite(path))
                {
                    stream.Write(RandomNumberGenerator.GetBytes((int)Math.Min(length, int.MaxValue)));
                }
                File.Delete(path);
            }
            var dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { /* best effort - file may still be open in the external app */ }
    }
}
