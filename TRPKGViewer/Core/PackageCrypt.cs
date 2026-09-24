using System;
using System.Security.Cryptography;
using System.Text;

namespace TRPKGViewer.Core
{
    // Reconstructed from the package-crypt constructor used by the active reader.
    // Validated against script\\abnormalitymode\\abnormalitymodedata.xml:
    // decrypted output begins EF BB BF "<?xml version=..."
    public sealed class PackageCrypt
    {
        static readonly byte[] FixedKey = { 0x0D,0x68,0x07,0x6F,0x0A,0x09,0x07,0x6C,0x65,0x73,0x0D,0x75,0x6E,0x0A,0x65,0x0D };
        static readonly byte[] Seed = Encoding.ASCII.GetBytes("CECA72E7ACDB485C8DF0AC16A2EA6D92");
        readonly byte[] key;
        readonly byte[] tailMask;

        public PackageCrypt()
        {
            key = new byte[32];
            Transform(FixedKey, Seed, 0, key, 0, false);
            Transform(FixedKey, Seed, 16, key, 16, false);
            tailMask = new byte[16];
            Transform(key, key, 0, tailMask, 0, false);
        }

        public byte[] Decrypt(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            byte[] result = (byte[])data.Clone();
            int full = result.Length / 16 * 16;
            if (full != 0)
            {
                byte[] tmp = new byte[full];
                using (var aes = Create(key))
                using (var d = aes.CreateDecryptor())
                    d.TransformBlock(result, 0, full, tmp, 0);
                Buffer.BlockCopy(tmp, 0, result, 0, full);
            }
            for (int i = full; i < result.Length; i++)
                result[i] ^= tailMask[i - full];
            return result;
        }

        static void Transform(byte[] k, byte[] input, int inputOffset, byte[] output, int outputOffset, bool decrypt)
        {
            using (var aes = Create(k))
            using (var t = decrypt ? aes.CreateDecryptor() : aes.CreateEncryptor())
                t.TransformBlock(input, inputOffset, 16, output, outputOffset);
        }

        static Aes Create(byte[] k)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = k;
            return aes;
        }
    }
}
