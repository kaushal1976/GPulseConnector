using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class AesEncryption
{
    // Must match your main app's Key and IV
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("Your32CharLongKeyHere_1234567890"); // 32 bytes
    private static readonly byte[] IV  = Encoding.UTF8.GetBytes("Your16CharIVHere");      

    public static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("Enter your SQL Server connection string:");
        var plainConn = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(plainConn))
        {
            Console.WriteLine("Connection string cannot be empty!");
            return;
        }

        var encrypted = AesEncryption.Encrypt(plainConn);
        Console.WriteLine("\nEncrypted connection string (Base64):");
        Console.WriteLine(encrypted);
    }
}
