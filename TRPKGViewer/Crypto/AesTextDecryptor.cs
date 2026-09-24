using System;
using System.Security.Cryptography;

namespace TRPKGViewer.Crypto
{
    // Text decryption is deliberately independent from package parsing.
    // Register one implementation per client version (for example "2026").
    public sealed class AesTextDecryptor : ITextDecryptor
    {
        readonly byte[] key;
        readonly byte[] iv;

        public string Version { get; private set; }
        public string Name { get { return Version + " / AES"; } }

        public AesTextDecryptor(string version, byte[] key, byte[] iv = null)
        {
            Version = version;
            this.key = key ?? throw new ArgumentNullException("key");
            this.iv = iv ?? new byte[16];
        }

        public byte[] Decrypt(byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var decryptor = aes.CreateDecryptor())
                    return decryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        public static AesTextDecryptor FromHex(string version, string hexKey)
        {
            if (hexKey == null || hexKey.Length % 2 != 0) throw new ArgumentException("Invalid hex key");
            var k = new byte[hexKey.Length / 2];
            for (int i = 0; i < k.Length; i++) k[i] = Convert.ToByte(hexKey.Substring(i * 2, 2), 16);
            return new AesTextDecryptor(version, k);
        }
    }
}