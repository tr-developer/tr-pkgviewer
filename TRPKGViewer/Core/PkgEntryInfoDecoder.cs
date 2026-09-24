using System;
using System.IO;
using System.IO.Compression;

namespace TRPKGViewer.Core
{
    // Mirrors the entry-info transform used by CUMTPackageReader::open.
    // The dump calls a zlib-compatible uncompress2-style routine with
    // output capacity 0x51C and an input-length pointer.
    public static class PkgEntryInfoDecoder
    {
        public const int DecodedSize = 0x51C;
        public const int MaxEncodedSize = 0x8F2;

        public static byte[] DecodeZlib(byte[] encoded)
        {
            if (encoded == null) throw new ArgumentNullException("encoded");
            if (encoded.Length == 0 || encoded.Length > MaxEncodedSize)
                throw new InvalidDataException("Invalid encoded entry-info size.");
            if (encoded.Length < 6)
                throw new InvalidDataException("Truncated zlib stream.");

            // RFC1950: CMF/FLG + RFC1951 payload + Adler32.
            int cmf = encoded[0], flg = encoded[1];
            if ((cmf & 0x0F) != 8 || ((cmf << 8) + flg) % 31 != 0)
                throw new InvalidDataException("Entry-info block is not a valid zlib stream.");
            if ((flg & 0x20) != 0)
                throw new InvalidDataException("Preset zlib dictionary is not supported.");

            using (var input = new MemoryStream(encoded, 2, encoded.Length - 6, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(DecodedSize))
            {
                var buffer = new byte[1024];
                int n;
                while ((n = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, n);
                    if (output.Length > DecodedSize)
                        throw new InvalidDataException("Decoded entry-info exceeds 0x51C bytes.");
                }

                byte[] decoded = output.ToArray();
                if (decoded.Length != DecodedSize)
                    throw new InvalidDataException("Invalid CUMTPackageFileEntryInfo size: " + decoded.Length);
                return decoded;
            }
        }
    }
}