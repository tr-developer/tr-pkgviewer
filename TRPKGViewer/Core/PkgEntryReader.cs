using System;
using System.IO;
using System.IO.Compression;

namespace TRPKGViewer.Core
{
    public sealed class PkgEntryReader
    {
        const int ChunkHeaderSize = 0x14;
        const int MaxChunkSize = 0x19000;

        public byte[] Read(string packagePath, PkgIndexEntry entry)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            using (var fs = File.OpenRead(packagePath))
            using (var output = new MemoryStream(checked((int)entry.LogicalSize)))
            {
                long pos = entry.DataOffset;
                int chunkIndex = 0;
                while (output.Length < entry.LogicalSize)
                {
                    fs.Position = pos;
                    byte[] h = new byte[ChunkHeaderSize];
                    ReadExact(fs, h, 0, h.Length);
                    uint index = BitConverter.ToUInt32(h, 0);
                    int unpacked = checked((int)BitConverter.ToUInt32(h, 4));
                    int stored = checked((int)BitConverter.ToUInt32(h, 8));
                    uint value0C = BitConverter.ToUInt32(h, 12);
                    uint flags = BitConverter.ToUInt32(h, 16);
                    if (index != (uint)chunkIndex) throw new InvalidDataException("Invalid chunk index.");
                    if (unpacked < 0 || unpacked > MaxChunkSize || stored < 0 || pos + ChunkHeaderSize + stored > fs.Length)
                        throw new InvalidDataException("Invalid chunk size.");
                    byte[] payload = new byte[stored];
                    ReadExact(fs, payload, 0, stored);
                    byte[] data = (flags & 1) != 0 ? InflateZlib(payload, unpacked) : payload;
                    if ((flags & 2) != 0)
                        throw new PackageCryptRequiredException(entry.Name, chunkIndex, pos, unpacked, stored, value0C, flags);
                    if (data.Length != unpacked) throw new InvalidDataException("Chunk unpacked-size mismatch.");
                    output.Write(data, 0, data.Length);
                    pos += ChunkHeaderSize + stored;
                    chunkIndex++;
                }
                if (output.Length != entry.LogicalSize) throw new InvalidDataException("Entry logical-size mismatch.");
                return output.ToArray();
            }
        }

        static byte[] InflateZlib(byte[] encoded, int expected)
        {
            if (encoded.Length < 6) throw new InvalidDataException("Truncated zlib chunk.");
            using (var input = new MemoryStream(encoded, 2, encoded.Length - 6, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(expected))
            {
                deflate.CopyTo(output);
                byte[] b = output.ToArray();
                if (b.Length != expected) throw new InvalidDataException("Chunk zlib-size mismatch.");
                return b;
            }
        }

        static void ReadExact(Stream s, byte[] b, int o, int n)
        {
            while (n > 0) { int r = s.Read(b, o, n); if (r <= 0) throw new EndOfStreamException(); o += r; n -= r; }
        }
    }

    public sealed class PackageCryptRequiredException : NotSupportedException
    {
        public PackageCryptRequiredException(string name, int index, long offset, int unpacked, int stored, uint value0C, uint flags)
            : base("Package-crypt chunk: " + name + " #" + index + " offset=0x" + offset.ToString("X") +
                   " unpacked=" + unpacked + " stored=" + stored + " value0C=0x" + value0C.ToString("X8") +
                   " flags=0x" + flags.ToString("X8")) { }
    }
}
