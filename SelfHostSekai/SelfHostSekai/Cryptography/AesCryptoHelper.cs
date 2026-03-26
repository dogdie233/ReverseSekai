using System.Security.Cryptography;
using System.Text;

namespace SelfHostSekai.Cryptography;

public class AesCryptoHelper
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesCryptoHelper(string hexKey, string hexIv)
    {
        _key = Convert.FromHexString(hexKey);
        _iv = Convert.FromHexString(hexIv);
    }

    public byte[] Encrypt(byte[] plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plainText, 0, plainText.Length);
    }

    public byte[] Decrypt(byte[] cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }
}