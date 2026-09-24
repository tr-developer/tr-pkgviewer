using System;
using System.Text;

namespace TRPKGViewer.Crypto
{
    // Key material is known, but the encrypted-TXT mode/IV/framing has not yet
    // been recovered from a matching client call site. Do not guess a cipher mode.
    public sealed class AesTextDecryptor : ITextDecryptor
    {
        readonly byte[] key;
        public string Name { get { return "aeskey"; } }

        public AesTextDecryptor(byte[] key)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must be 16, 24 or 32 bytes.");
            this.key = (byte[])key.Clone();
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null) throw new ArgumentNullException("encryptedData");
            throw new NotSupportedException(
                "aeskey text decrypt framing is not verified yet; refusing to apply guessed CBC/IV/padding.");
        }

        public static AesTextDecryptor FromKeyString(string keyText)
        {
            if (string.IsNullOrEmpty(keyText)) throw new ArgumentException("AES key is empty.");
            byte[] key;
            if (IsHex(keyText))
            {
                key = new byte[keyText.Length / 2];
                for (int i = 0; i < key.Length; i++)
                    key[i] = Convert.ToByte(keyText.Substring(i * 2, 2), 16);
            }
            else key = Encoding.UTF8.GetBytes(keyText);
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
