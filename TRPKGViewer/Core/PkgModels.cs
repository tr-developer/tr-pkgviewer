using System.Collections.Generic;

namespace TRPKGViewer.Core
{
    public sealed class PkgInfo
    {
        public string Path;
        public long Length;
        public byte[] Header;
        public List<PkgWord> Words = new List<PkgWord>();
        public List<AsciiRun> Strings = new List<AsciiRun>();
    }

    public sealed class PkgWord { public int Offset; public uint Value; }
    public sealed class AsciiRun { public long Offset; public string Text; }
}