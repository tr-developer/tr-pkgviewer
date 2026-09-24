using System;
using System.Collections.Generic;
using System.Linq;

namespace TRPKGViewer.Crypto
{
    public sealed class TextDecryptorRegistry
    {
        readonly List<ITextDecryptor> list = new List<ITextDecryptor>();
        public IEnumerable<ITextDecryptor> All { get { return list; } }

        public void Register(ITextDecryptor d)
        {
            if (d == null) throw new ArgumentNullException("d");
            list.RemoveAll(x => x.Version == d.Version);
            list.Add(d);
        }

        public ITextDecryptor Get(string version)
        {
            return list.FirstOrDefault(x => x.Version == version);
        }
    }
}