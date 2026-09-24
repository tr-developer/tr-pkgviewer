# TRPKGViewer

C# / .NET Framework 4.8 / WinForms viewer for TR PKG files.

## Current reverse-engineering findings

The supplied game dump contains RTTI / diagnostic strings for `CUMTPackageReader`, `CUMTPackageFileEntryReader`, `CUMTPackageCryptBase`, `CUMTPackageCrypt`, and `CUMTPackageWriter`, plus `Invalid CUMTPackageFileEntryInfo size!`. This strongly indicates a custom UMT package path rather than ZIP-compatible archive handling.

A nearby package-crypt area contains the 32-character constant `A73FF15DBE914FCC880037FB32E4D314`. The project records that as a reverse-engineering lead only; it does not assume that it is a text AES key.

The sample `tr4.pkg` begins with binary metadata and a simple plaintext scan does not reveal a reliable file-name table. For that reason, the initial parser does not invent entry offsets or structures. It provides header, DWORD, hex and printable-string diagnostics while the exact `CUMTPackageReader` layout is recovered.

## Text decrypt architecture

Text decryption is intentionally independent from PKG parsing:

- `ITextDecryptor`
- `TextDecryptorRegistry`
- `AesTextDecryptor`

Keys are registered by version (for example `2026`). AES mode/IV/KDF can be replaced per version without changing the package parser.

## Build

Open `TRPKGViewer.sln` in Visual Studio with the .NET Framework 4.8 Developer Pack installed and build the solution.

## Next reverse-engineering target

Recover the concrete `CUMTPackageReader::open` / `CUMTPackageFileEntryReader` field layout from the supplied dump, then implement file table enumeration and extraction without guessed offsets.
