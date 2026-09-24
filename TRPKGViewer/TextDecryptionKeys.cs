using System;
using System.Collections.Generic;

namespace TRPKGViewer
{
    public sealed class TextDecryptionKey
    {
        public string Name { get; private set; }
        public string Key { get; private set; }

        internal TextDecryptionKey(string name, string key)
        {
            Name = name;
            Key = key;
        }
    }

    public static class TextDecryptionKeys
    {
        static readonly List<TextDecryptionKey> keys = new List<TextDecryptionKey>();

        public static IList<TextDecryptionKey> All { get { return keys.AsReadOnly(); } }

        static TextDecryptionKeys()
        {
            // Add text-decryption keys here. Example:
            // AddKey("Example", "0123456789ABCDEF0123456789ABCDEF");
        }

        public static void AddKey(string name, string key)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Key name is required.", "name");
            if (key == null) throw new ArgumentNullException("key");
            keys.Add(new TextDecryptionKey(name, key));
        }
    }
}
