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

            for (int i = 0; i < info.Probe.EntryCount; i++)
            {
                byte[] sizeBytes = new byte[4];
                ReadExact(fs, sizeBytes, 0, 4);
                int encodedSize = checked((int)BitConverter.ToUInt32(sizeBytes, 0));
                if (encodedSize <= 0 || encodedSize > PkgEntryInfoDecoder.MaxEncodedSize)
                    throw new InvalidDataException("Invalid encoded entry-info size at index " + i + ".");

                byte[] encoded = new byte[encodedSize];
                ReadExact(fs, encoded, 0, encoded.Length);
                byte[] decoded = PkgEntryInfoDecoder.DecodeZlib(encoded);

                int nameLength = 0;
                while (nameLength < 0x80 && decoded[nameLength] != 0) nameLength++;
                string name = Encoding.ASCII.GetString(decoded, 0, nameLength).ToLowerInvariant();
                info.Entries.Add(new PkgIndexEntry { Index = i, Name = name, EncodedInfoSize = encodedSize, DecodedInfo = decoded });
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