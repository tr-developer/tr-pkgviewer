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
        public List<PkgIndexEntry> Entries = new List<PkgIndexEntry>();
    }

    public sealed class PkgHeaderProbe
    {
        public uint Version;
        public uint IndexOffset;
        public uint HeaderSize;
        public uint DecodedEntryInfoSize;
        public uint EntryCount;
        public uint EntryCountMirror;
    }

    public sealed class PkgIndexEntry
    {
        public int Index;
        public string Name;
        public int EncodedInfoSize;
        public byte[] DecodedInfo;
        public uint LogicalSize;
        public uint Decoded408;
        public uint Decoded40C;
        public uint Decoded410;
        public uint DataOffset;
    }

    public sealed class PkgEntryInfo
    {
        public const int MaterializedSize = 0xE5;
        public const int NameSize = 0x80;
        public string Name;
        public byte Flag80;
        public uint Value84, Value88, Value8C, Value90, Value94, Value98;
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