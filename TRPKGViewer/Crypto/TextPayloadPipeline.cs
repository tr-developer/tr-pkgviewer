using System;
using System.IO;

namespace TRPKGViewer.Crypto
{
    // Single gate for text payloads. Callers must not bypass this for encrypted TXT.
    public sealed class TextPayloadPipeline
    {
        readonly TextDecryptorRegistry decryptors;
        public TextPayloadPipeline(TextDecryptorRegistry decryptors)
        {
            this.decryptors = decryptors ?? throw new ArgumentNullException("decryptors");
        }

        public byte[] Process(string entryName, bool encrypted, byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException("payload");
            if (!IsTxt(entryName) || !encrypted) return payload;
            return decryptors.DecryptEncryptedText(payload);
        }

        static bool IsTxt(string name)
        {
            return string.Equals(Path.GetExtension(name ?? ""), ".txt", StringComparison.OrdinalIgnoreCase);
        }
    }
}