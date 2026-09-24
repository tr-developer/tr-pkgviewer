using System;
using System.Security.Cryptography;
using System.Text;

namespace TRPKGViewer.Crypto
{
    // "aeskey" decrypt type. Replace/extend this class when the real game's
    // AES mode, IV derivation or key material is recovered.
    public sealed class AesTextDecryptor : ITextDecryptor
    {
        readonly byte[] key;
        readonly byte[] iv;
        public string Name { get { return "aeskey"; } }

        public AesTextDecryptor(byte[] key, byte[] iv = null)
        {
            this.key = key ?? throw new ArgumentNullException("key");
            this.iv = iv ?? new byte[16];
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null) throw new ArgumentNullException("encryptedData");
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var decryptor = aes.CreateDecryptor())
                    return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            }
        }

        public static AesTextDecryptor FromKeyString(string keyText)
        {
            if (string.IsNullOrEmpty(keyText)) throw new ArgumentException("AES key is empty.");
            byte[] key;
            if (IsHex(keyText))
            {
                key = new byte[keyText.Length / 2];
                for (int i = 0; i < key.Length; i++) key[i] = Convert.ToByte(keyText.Substring(i * 2, 2), 16);
            }
            else key = Encoding.UTF8.GetBytes(keyText);

            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must be 16, 24 or 32 bytes.");
            return new AesTextDecryptor(key);
        }

        static bool IsHex(string s)
        {
            if (s.Length == 0 || s.Length % 2 != 0) return false;
            for (int i = 0; i < s.Length; i++)
                if (!Uri.IsHexDigit(s[i])) return false;
            return true;
        }
    }
}