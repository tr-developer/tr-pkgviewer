using System.Collections.Generic;

namespace TRPKGViewer.Core
{
    public sealed class PkgInfo
    {
        public string Path;
        public long Length;
        public byte[] Header;
        public List<PkgWord> Words = new List<PkgWord>();
        public List<AsciiRun> Strings = new List<AsciiRun>();\n        public PkgHeaderProbe Probe;
    }

    public sealed class PkgHeaderProbe\n    {\n        public uint Value0C;\n        public uint Value14;\n        public uint Value18;\n        public uint Value1C;\n        public uint Value24;\n        public uint Value28;\n    }\n\n    public sealed class PkgWord { public int Offset; public uint Value; }
    public sealed class AsciiRun { public long Offset; public string Text; }
}