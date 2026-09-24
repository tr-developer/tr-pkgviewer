using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TRPKGViewer.Core
{
    public sealed class PkgReader
    {
        public PkgInfo Open(string path)
        {
            var fi = new FileInfo(path);
            var info = new PkgInfo { Path = path, Length = fi.Length };
            using (var fs = File.OpenRead(path))
            {
                int n = (int)Math.Min(4096, fs.Length);
                info.Header = new byte[n];
                ReadExact(fs, info.Header, 0, n);
                for (int o = 0; o + 4 <= Math.Min(n, 256); o += 4)
                    info.Words.Add(new PkgWord { Offset = o, Value = BitConverter.ToUInt32(info.Header, o) });

                if (n < 0x2C) throw new InvalidDataException("Truncated package header.");
                info.Probe = new PkgHeaderProbe
                {
                    Version = BitConverter.ToUInt32(info.Header, 0x0C),
                    IndexOffset = BitConverter.ToUInt32(info.Header, 0x14),
                    HeaderSize = BitConverter.ToUInt32(info.Header, 0x18),
                    DecodedEntryInfoSize = BitConverter.ToUInt32(info.Header, 0x1C),
                    EntryCount = BitConverter.ToUInt32(info.Header, 0x24),
                    EntryCountMirror = BitConverter.ToUInt32(info.Header, 0x28)
                };
                if (info.Probe.HeaderSize != 0x1C) throw new InvalidDataException("Invalid package header size.");
                if (info.Probe.DecodedEntryInfoSize != PkgEntryInfoDecoder.DecodedSize) throw new InvalidDataException("Invalid entry-info size.");
                if (info.Probe.IndexOffset >= fs.Length) throw new InvalidDataException("Invalid package index offset.");
                // +0x24 is the index entry count used by the reader.
                // +0x28 is retained for diagnostics only; it is not required to mirror +0x24
                // across all package variants.
                if (info.Probe.EntryCount == 0)
                    throw new InvalidDataException("Package contains no index entries.");
                long minimumIndexBytes = 12L + (long)info.Probe.EntryCount * 5L;
                if ((long)info.Probe.IndexOffset + minimumIndexBytes > fs.Length)
                    throw new InvalidDataException("Package entry count exceeds the available index region: " +
                        info.Probe.EntryCount + ".");
                ReadIndex(fs, info);

                fs.Position = 0;
                info.Strings = ScanAscii(fs, 6, 5000);
            }
            return info;
        }

        static void ReadIndex(FileStream fs, PkgInfo info)
        {
            fs.Position = info.Probe.IndexOffset;
            byte[] indexHeader = new byte[12];
            ReadExact(fs, indexHeader, 0, indexHeader.Length);
            if (BitConverter.ToUInt32(indexHeader, 0) != 0)
                throw new InvalidDataException("Invalid package index header.");

            int expectedCount = checked((int)info.Probe.EntryCount);
            for (int i = 0; i < expectedCount; i++)
            {
                if (fs.Position + 4 > fs.Length)
                {
                    // Some package variants carry a larger header-side count.  The on-disk
                    // index itself is authoritative: reaching EOF exactly after a complete
                    // entry terminates the index cleanly.
                    if (fs.Position == fs.Length && i > 0) break;
                    throw new InvalidDataException("Index ended before entry " + i +
                        " of header count " + info.Probe.EntryCount + " (position 0x" +
                        fs.Position.ToString("X") + ", file size 0x" + fs.Length.ToString("X") + ").");
                }

                byte[] sizeBytes = new byte[4];
                ReadExact(fs, sizeBytes, 0, 4);
                int encodedSize = checked((int)BitConverter.ToUInt32(sizeBytes, 0));
                if (encodedSize <= 0 || encodedSize > PkgEntryInfoDecoder.MaxEncodedSize)
                    throw new InvalidDataException("Invalid encoded entry-info size at index " + i + ".");

                if (fs.Position + encodedSize > fs.Length)
                    throw new InvalidDataException("Encoded entry-info at index " + i +
                        " exceeds end of file (size " + encodedSize + ", position 0x" +
                        fs.Position.ToString("X") + ", file size 0x" + fs.Length.ToString("X") + ").");

                byte[] encoded = new byte[encodedSize];
                ReadExact(fs, encoded, 0, encoded.Length);
                byte[] decoded = PkgEntryInfoDecoder.DecodeZlib(encoded);

                int nameLength = 0;
                while (nameLength < 0x80 && decoded[nameLength] != 0) nameLength++;
                string name = Encoding.ASCII.GetString(decoded, 0, nameLength).ToLowerInvariant();
                info.Entries.Add(new PkgIndexEntry
                {
                    Index = i, Name = name, EncodedInfoSize = encodedSize, DecodedInfo = decoded,
                    LogicalSize = BitConverter.ToUInt32(decoded, 0x404),
                    Decoded408 = BitConverter.ToUInt32(decoded, 0x408),
                    Decoded40C = BitConverter.ToUInt32(decoded, 0x40C),
                    Decoded410 = BitConverter.ToUInt32(decoded, 0x410),
                    DataOffset = BitConverter.ToUInt32(decoded, 0x414)
                });
            }
        }

        public byte[] ReadRange(string path, long offset, int count)
        {
            using (var fs = File.OpenRead(path))
            {
                if (offset < 0 || offset > fs.Length) throw new ArgumentOutOfRangeException("offset");
                fs.Position = offset;
                int n = (int)Math.Min(count, fs.Length - offset);
                var b = new byte[n];
                ReadExact(fs, b, 0, n);
                return b;
            }
        }

        static void ReadExact(Stream s, byte[] b, int o, int n)
        {
            while (n > 0)
            {
                int r = s.Read(b, o, n);
                if (r <= 0) throw new EndOfStreamException();
                o += r; n -= r;
            }
        }

        static List<AsciiRun> ScanAscii(Stream s, int min, int max)
        {
            var result = new List<AsciiRun>();
            long pos = 0, start = -1;
            var sb = new StringBuilder();
            int v;
            while ((v = s.ReadByte()) >= 0 && result.Count < max)
            {
                bool ok = v >= 0x20 && v <= 0x7e;
                if (ok)
                {
                    if (start < 0) start = pos;
                    sb.Append((char)v);
                }
                else
                {
                    if (sb.Length >= min) result.Add(new AsciiRun { Offset = start, Text = sb.ToString() });
                    sb.Clear(); start = -1;
                }
                pos++;
            }
            if (sb.Length >= min) result.Add(new AsciiRun { Offset = start, Text = sb.ToString() });
            return result;
        }

        public static string Hex(byte[] b, long baseOffset = 0)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < b.Length; i += 16)
            {
                sb.AppendFormat("{0:X8}  ", baseOffset + i);
                for (int j = 0; j < 16; j++) sb.Append(j + i < b.Length ? b[i + j].ToString("X2") + " " : "   ");
                sb.Append(" ");
                for (int j = 0; j < 16 && i + j < b.Length; j++)
                {
                    byte x = b[i + j];
                    sb.Append(x >= 32 && x < 127 ? (char)x : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}