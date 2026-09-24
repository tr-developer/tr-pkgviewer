# TRPKGViewer

C# / .NET Framework 4.8 / WinForms viewer for TR PKG files.

## Current reverse-engineering findings

The supplied game dump contains RTTI / diagnostic strings for `CUMTPackageReader`, `CUMTPackageFileEntryReader`, `CUMTPackageCryptBase`, `CUMTPackageCrypt`, and `CUMTPackageWriter`, plus `Invalid CUMTPackageFileEntryInfo size!`. This strongly indicates a custom UMT package path rather than ZIP-compatible archive handling.

The package-crypt class starts from the 32-byte ASCII constant `A73FF15DBE914FCC880037FB32E4D314`. This belongs to `CUMTPackageCrypt` / package chunk processing and is not the encrypted-TXT AES key. Important correction: the constructor can preprocess the two 16-byte halves through another package-crypt context before building the per-key cipher state, so the ASCII constant must not be assumed to be the final raw block-cipher key. The chunk reader calls the crypt object only after optional zlib expansion, so the transform operates on the unpacked chunk buffer. The virtual crypt call receives the chunk buffer plus its unpacked byte count; no file-path text is passed at that call site.

The supplied `tr4.pkg` is 96,701,627 bytes. The dump now confirms several header semantics rather than merely suggesting them:

- file `+0x0C`: package version; the sample is version `2`
- file `+0x14`: absolute index/metadata seek offset; the sample points to `96,259,453`
- file `+0x18`: package header size; the reader requires exactly `0x1C`
- file `+0x1C`: decoded entry-info size; the reader requires exactly `0x51C`

The first 12 bytes are a package signature. For version 2 the client XORs those 12 bytes with a fixed package-crypt byte sequence and then compares the result to ASCII `ACAC35E5-4B7`. From the supplied sample, the corresponding 12-byte XOR sequence is `05 5B CB 64 FB C2 CE B4 77 8B 1B B8`. This is package-format crypt and is kept separate from encrypted TXT decryption.

At the index offset, the sample contains a 12-byte index header followed immediately by `[uint32 encodedSize][zlib entry-info]` records. Header fields `+0x24` and `+0x28` both contain `473`; decoding exactly 473 records succeeds and produces real paths such as `script\\abnormalitymode\\abnormalitymodedata.xml`, `script\\abusedata.txt`, and `script\\advertisement.txt`. `PkgReader` now enumerates these entries and the WinForms tree displays all recovered names. For that reason, the initial parser does not invent entry offsets or structures. It provides header, DWORD, hex and printable-string diagnostics while the exact `CUMTPackageReader` layout is recovered.

## Text decrypt architecture

Text decryption is intentionally independent from PKG parsing:

- `ITextDecryptor`
- `TextDecryptorRegistry`
- `AesTextDecryptor`

Encrypted TXT entries are always routed through `TextPayloadPipeline` and text decryption before preview/export. This is the single gate intended for the eventual entry reader/extractor, so encrypted TXT handling is not optional. The extension point is the decrypt **type**, not a version-to-key mapping. `aeskey` is the initial type. Additional algorithms can be added by implementing `ITextDecryptor` and registering the new type. Package parsing remains independent from text decryption.

## Build

Open `TRPKGViewer.sln` in Visual Studio with the .NET Framework 4.8 Developer Pack installed and build the solution.

## Recovered CUMTPackageReader entry path

Static analysis of the supplied dump located the code path that logs `CUMTPackageReader::open(%s)... %dms`.

The reader repeatedly:

1. reads a 32-bit encoded block length,
2. rejects zero or values above `0x8F2`,
3. decompresses that block through a zlib `uncompress2`-style routine into a fixed `0x51C`-byte entry-info buffer,
4. validates that the resulting size is exactly `0x51C`,
5. lower-cases the path/name contained in the decoded buffer,
6. materializes an internal entry record with a stride of exactly `0xE5` bytes.

The recovered materialized record begins with a `0x80`-byte name/path area. The source-to-materialized mapping is now exact: decoded `+0x400 -> materialized +0x80`, `+0x404 -> +0x84`, `+0x408 -> +0x88`, `+0x40C -> +0x8C`, `+0x410 -> +0x90`, `+0x414 -> +0x94`, `+0x418 -> +0x98`, `+0x41C..0x433 -> +0x9C..0xB3`, `+0x434 -> +0xB4`, `+0x438..0x44F -> +0xB8..0xCF`, `+0x450 -> +0xD0`, `+0x50B -> +0xD4`, and `+0x50C..0x51B -> +0xD5..0xE4`. These are now represented by `PkgEntryInfo`; unknown fields remain neutrally named until their semantics are proven.

This also explains the dump string `Invalid CUMTPackageFileEntryInfo size!`: the decoded entry-info object is expected to be `0x51C` bytes.

## Next reverse-engineering target

The package header and version-2 signature XOR path are now recovered, including the absolute index seek at file `+0x14`. Across all decoded `0x51C` records, the meaningful tail data is concentrated around decoded offsets `0x404..0x417` and `0x50B..0x51B`. The viewer now exposes the DWORDs at `0x404`, `0x408`, `0x40C`, `0x410`, and `0x414` per file for comparison. A direct scan of all 473 decoded records against the supplied package gives stronger constraints: decoded `+0x404` ranges from 18 to 57,965,868 and `+0x408` from 38 to 22,562,731, with both always below the package length and behaving like size-related values. Decoded `+0x40C` has 470 distinct values and only 302/473 are below the package length, so it is definitively not a plain absolute file offset. Decoded `+0x410` is usually 1 (437/473) but has several small alternate values, making it a likely mode/flag/count field rather than a byte size. Byte `+0x50B` is exactly 1 for all 473 entries, followed by a 16-byte per-entry value at `+0x50C..0x51B`; this tail is now a prime package-crypt/integrity candidate. Field semantics remain unlabelled until the corresponding reader routine is traced. The encoded entry-info transform is zlib-compatible decompression and is implemented by `PkgEntryInfoDecoder`. Further disassembly of the package file-entry reader recovered its block-cache format. The reader works in logical blocks of exactly `0x19000` (102,400) bytes. Each cached block record is `0x30` bytes and the on-disk block header is exactly `0x14` bytes. The block header contains five 32-bit values: block index, stored payload size (validated <= `0x19000`), logical/unpacked block size, and two additional integrity/crypt fields. The reader computes each next block's physical position as `previousBlockOffset + previousStoredPayloadSize + 0x14`; the first block starts from a base data offset held by the file-entry reader. Reads then copy from `cachedBlockPayload + 0x10 + (logicalPosition % 0x19000)`, confirming a 16-byte prefix in the decoded/cache payload.

This is a major constraint on the remaining mapping: the per-entry metadata must provide the base data offset and logical file size used to initialize the file-entry reader; `+0x40C` is not that raw offset. The next target is the constructor/open path that transfers fields from the materialized `0xE5` entry into the file-entry reader, which should identify the base offset and compression/crypt mode directly. Once those semantics are proven, wire the entry table into the tree and extractor.
