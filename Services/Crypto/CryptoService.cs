using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;

namespace PasswordVault.Services.Crypto;

public interface ICryptoService
{
    string DeriveKeyFromPassword(string password, byte[] salt);
    string EncryptPassword(string plaintext);
    string DecryptPassword(string ciphertext);
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] ciphertext);
    byte[] GenerateRandomBytes(int length);
}

public class CryptoService : ICryptoService
{
    private const int NONCE_LEN = 12;
    private const int TAG_LEN = 16;
    private const int KEY_LEN = 32;

    private readonly byte[] _encryptionKey;

    public CryptoService(byte[] encryptionKey)
    {
        if (encryptionKey?.Length != KEY_LEN)
            throw new ArgumentException($"Key must be {KEY_LEN} bytes", nameof(encryptionKey));

        _encryptionKey = encryptionKey;
    }

    public string DeriveKeyFromPassword(string password, byte[] salt)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password empty.", nameof(password));
        if (salt is null || salt.Length == 0)
            throw new ArgumentException("Salt empty.", nameof(salt));

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.DegreeOfParallelism = 4;
        argon2.Iterations = 3;
        argon2.MemorySize = 65_536; // 64 MB

        byte[] key = argon2.GetBytes(KEY_LEN);
        return Convert.ToBase64String(key);
    }

    public string EncryptPassword(string plaintext) =>
        Convert.ToBase64String(Encrypt(plaintext));

    public string DecryptPassword(string ciphertext) =>
        Decrypt(Convert.FromBase64String(ciphertext));


    public byte[] Encrypt(string plaintext)
    {
        byte[] nonce = GenerateRandomBytes(NONCE_LEN);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipher = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TAG_LEN];

        using var aes = new AesGcm(_encryptionKey, TAG_LEN);
        aes.Encrypt(nonce, plaintextBytes, cipher, tag);

        var result = new byte[NONCE_LEN + TAG_LEN + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NONCE_LEN);
        Buffer.BlockCopy(tag, 0, result, NONCE_LEN, TAG_LEN);
        Buffer.BlockCopy(cipher, 0, result, NONCE_LEN + TAG_LEN, cipher.Length);
        return result;
    }

    public string Decrypt(byte[] ciphertext)
    {
        if (ciphertext.Length < NONCE_LEN + TAG_LEN)
            throw new ArgumentException("Ciphertext too short.", nameof(ciphertext));

        // split
        byte[] nonce = ciphertext[..NONCE_LEN];
        byte[] tag = ciphertext[NONCE_LEN..(NONCE_LEN + TAG_LEN)];
        byte[] cipher = ciphertext[(NONCE_LEN + TAG_LEN)..];

        byte[] plain = new byte[cipher.Length];
        using var aes = new AesGcm(_encryptionKey, TAG_LEN);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    public byte[] GenerateRandomBytes(int length)
    {
        var randomBytes = new byte[length];
        RandomNumberGenerator.Fill(randomBytes);
        return randomBytes;
    }
}
