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
        public PkgHeaderProbe Probe;
    }

    public sealed class PkgHeaderProbe
    {
        public uint Value0C;
        public uint Value14;
        public uint Value18;
        public uint Value1C;
        public uint Value24;
        public uint Value28;
    }

    // Recovered from CUMTPackageReader::open in the supplied dump.
    // The reader materializes each entry into a packed 0xE5-byte record.
    public sealed class PkgEntryInfo
    {
        public const int MaterializedSize = 0xE5;
        public const int NameSize = 0x80;
        public string Name;
        public byte Flag80;
        public uint Value84;
        public uint Value88;
        public uint Value8C;
        public uint Value90;
        public uint Value94;
        public uint Value98;
        public byte[] Block9C = new byte[0x18];
        public uint ValueB4;
        public byte[] BlockB8 = new byte[0x18];
        public uint ValueD0;
        public byte FlagD4;
        public byte[] TailD5 = new byte[0x10];
    }

    public sealed class PkgWord { public int Offset; public uint Value; }
    public sealed class AsciiRun { public long Offset; public string Text; }
}