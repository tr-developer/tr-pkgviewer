# TRPKGViewer

C# / .NET Framework 4.8 / WinForms viewer for TR PKG files.

## Current reverse-engineering findings

The supplied game dump contains RTTI / diagnostic strings for `CUMTPackageReader`, `CUMTPackageFileEntryReader`, `CUMTPackageCryptBase`, `CUMTPackageCrypt`, and `CUMTPackageWriter`, plus `Invalid CUMTPackageFileEntryInfo size!`. This strongly indicates a custom UMT package path rather than ZIP-compatible archive handling.

A nearby package-crypt area contains the 32-character constant `A73FF15DBE914FCC880037FB32E4D314`. The project records that as a reverse-engineering lead only; it does not assume that it is a text AES key.

The supplied `tr4.pkg` is 96,701,627 bytes. The dump now confirms several header semantics rather than merely suggesting them:

- file `+0x0C`: package version; the sample is version `2`
- file `+0x14`: absolute index/metadata seek offset; the sample points to `96,259,453`
- file `+0x18`: package header size; the reader requires exactly `0x1C`
- file `+0x1C`: decoded entry-info size; the reader requires exactly `0x51C`

The first 12 bytes are a package signature. For version 2 the client XORs those 12 bytes with a fixed package-crypt byte sequence and then compares the result to ASCII `ACAC35E5-4B7`. From the supplied sample, the corresponding 12-byte XOR sequence is `05 5B CB 64 FB C2 CE B4 77 8B 1B B8`. This is package-format crypt and is kept separate from encrypted TXT decryption.

At the index offset, the reader consumes a 12-byte index header, requires its first DWORD to be zero, and bounds the following count/value to at most 1000 before allocating the `0xE5` entry array. For that reason, the initial parser does not invent entry offsets or structures. It provides header, DWORD, hex and printable-string diagnostics while the exact `CUMTPackageReader` layout is recovered.

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

The recovered materialized record begins with a `0x80`-byte name/path area. Additional copied fields are present at offsets `0x80`, `0x84`, `0x88`, `0x8C`, `0x90`, `0x94`, `0x98`, `0x9C..0xB3`, `0xB4`, `0xB8..0xCF`, `0xD0`, `0xD4`, and `0xD5..0xE4`. These are now represented by `PkgEntryInfo`; unknown fields remain neutrally named until their semantics are proven.

This also explains the dump string `Invalid CUMTPackageFileEntryInfo size!`: the decoded entry-info object is expected to be `0x51C` bytes.

## Next reverse-engineering target

The package header and version-2 signature XOR path are now recovered, including the absolute index seek at file `+0x14`. The encoded entry-info transform is zlib-compatible decompression and is implemented by `PkgEntryInfoDecoder`. The next target is decoding the 12-byte index header/count and then mapping the recovered `0xE5` fields to data offset, packed size, unpacked size and encryption/compression flags. Once those semantics are proven, wire the entry table into the tree and extractor.
