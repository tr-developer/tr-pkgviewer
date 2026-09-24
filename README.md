# TRPKGViewer

C# / .NET Framework 4.8 / WinForms viewer for TR PKG files.

## Current reverse-engineering findings

The supplied game dump contains RTTI / diagnostic strings for `CUMTPackageReader`, `CUMTPackageFileEntryReader`, `CUMTPackageCryptBase`, `CUMTPackageCrypt`, and `CUMTPackageWriter`, plus `Invalid CUMTPackageFileEntryInfo size!`. This strongly indicates a custom UMT package path rather than ZIP-compatible archive handling.

A nearby package-crypt area contains the 32-character constant `A73FF15DBE914FCC880037FB32E4D314`. The project records that as a reverse-engineering lead only; it does not assume that it is a text AES key.

The supplied `tr4.pkg` is 96,701,627 bytes. Its first DWORDs include `0x0C=2`, `0x14=96,259,453`, `0x18=28`, `0x1C=1308`, and matching values `0x24=0x28=473`. These are recorded as candidate header fields only; their semantics are not assumed until the reader code is recovered. The sample begins with binary metadata and a simple plaintext scan does not reveal a reliable file-name table. For that reason, the initial parser does not invent entry offsets or structures. It provides header, DWORD, hex and printable-string diagnostics while the exact `CUMTPackageReader` layout is recovered.

## Text decrypt architecture

Text decryption is intentionally independent from PKG parsing:

- `ITextDecryptor`
- `TextDecryptorRegistry`
- `AesTextDecryptor`

Encrypted TXT entries are always routed through `TextPayloadPipeline` and text decryption before preview/export. This is the single gate intended for the eventual entry reader/extractor, so encrypted TXT handling is not optional. The extension point is the decrypt **type**, not a version-to-key mapping. `aeskey` is the initial type. Additional algorithms can be added by implementing `ITextDecryptor` and registering the new type. Package parsing remains independent from text decryption.

## Build

Open `TRPKGViewer.sln` in Visual Studio with the .NET Framework 4.8 Developer Pack installed and build the solution.

## Next reverse-engineering target

Recover the concrete `CUMTPackageReader::open` / `CUMTPackageFileEntryReader` field layout from the supplied dump, then implement file table enumeration and extraction without guessed offsets.
