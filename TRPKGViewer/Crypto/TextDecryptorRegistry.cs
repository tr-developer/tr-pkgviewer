using System;
using System.Collections.Generic;
using System.Linq;

namespace TRPKGViewer.Crypto
{
    // Registry contains decrypt TYPES, not client-version/key mappings.
    // Encrypted text files are always passed through the selected decryptor.
    public sealed class TextDecryptorRegistry
    {
        readonly List<ITextDecryptor> list = new List<ITextDecryptor>();
        public IEnumerable<ITextDecryptor> All { get { return list; } }
        public ITextDecryptor Active { get; private set; }

        public void Register(ITextDecryptor decryptor, bool makeActive = false)
        {
            if (decryptor == null) throw new ArgumentNullException("decryptor");
            list.RemoveAll(x => string.Equals(x.Name, decryptor.Name, StringComparison.OrdinalIgnoreCase));
            list.Add(decryptor);
            if (Active == null || makeActive) Active = decryptor;
        }

        public void Select(string name)
        {
            var d = list.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (d == null) throw new InvalidOperationException("Unknown text decrypt type: " + name);
            Active = d;
        }

        public byte[] DecryptEncryptedText(byte[] encryptedData)
        {
            if (Active == null) throw new InvalidOperationException("No text decrypt type is registered.");
            return Active.Decrypt(encryptedData);
        }
    }
}