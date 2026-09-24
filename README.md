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

Encrypted TXT entries are always routed through `TextPayloadPipeline` and text decryption before preview/export. This is the single gate intended for the eventual entry reader/extractor, so encrypted TXT handling is not optional. The extension point is the decrypt **type**, not a version-to-key mapping. `aeskey` is the initial type. The supplied client contains the exact ASCII key string `CECA72E7ACDB485C8DF0AC16A2EA6D92` in `.rdata` (file offset `0x2A6CB78`, VA `0x7ff648e2d978`). Interpreted as hex, this is a 16-byte AES-128 key and the viewer now registers it as the default active text key. AES mode/IV handling remains subject to call-site verification; the key itself is confirmed present in the client. TXT crypto analysis is anchored on this literal and its client xrefs, independently of `CUMTPackageCrypt`. Additional algorithms can be added by implementing `ITextDecryptor` and registering the new type. Package parsing remains independent from text decryption.

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


### Package crypt initialization (verified from client disassembly)

The package-crypt constructor at `0x7ff646bbefc0` copies the 32 ASCII bytes
`A73FF15DBE914FCC880037FB32E4D314` from `.rdata`. If the crypt object's
preprocess flag at `+0x08` is set, each 16-byte half is transformed in-place
through the already-initialized cipher context at `+0x0C`. The resulting
32 bytes are then passed to the cipher initializer at `0x7ff64809d780` with
an explicit key length of `0x20`.

`0x7ff64809d780` clears exactly `0x10A0` bytes of expanded cipher state,
calls the key-schedule routine at `0x7ff6480ad360`, and derives the 16-byte
tail mask stored at context `+0x10A4` by encrypting the first 16 bytes of the
key with the newly-created context. The package reader later handles chunk
flag `0x2` by calling the decrypt wrapper at `0x7ff64809d990` only after
optional zlib expansion. Full 16-byte blocks use `0x7ff6480aef30`; the final
partial block is XORed with that derived tail mask.

This confirms that the visible 32-byte package constant is input key material,
but it must not be hard-coded as the final raw key until the object's `+0x08`
preprocess state and its `+0x0C` context initialization are recovered.


#### Package key preprocessing: constructor state resolved

A fresh disassembly pass resolves the earlier uncertainty around the constructor's `+0x08` byte. It is not an externally-selected boolean flag. The constructor first calls `0x7ff64809d840` on the cipher object beginning at object `+0x08`, using the fixed 16-byte binary value:

`0D 68 07 6F 0A 09 07 6C 65 73 0D 75 6E 0A 65 0D`

That initializer builds a 16-byte-key cipher context and sets its leading initialized byte to 1. The constructor immediately tests that same byte, so the two 16-byte halves of `A73FF15DBE914FCC880037FB32E4D314` are always passed through `0x7ff6480ae880` before the 32-byte package context is initialized.

Therefore the package-key preprocessing stage is mandatory for this client. A control test using ordinary OpenSSL AES-128-ECB for the first transform and AES-256-ECB for the package transform did **not** produce valid XML/TXT plaintext from real flag-`0x2` chunks. The client primitive must therefore be reproduced from its own key schedule/block routines rather than substituted with standard .NET/OpenSSL AES semantics.

The sample package contains 1,437 flag-`0x2` chunks, giving a large validation corpus once the primitive is ported.


#### Cipher primitive identification

Direct disassembly of `0x7ff6480ad360` shows the key setup accepts key sizes
`0x10`, `0x18`, or `0x20` bytes and computes the round-key word count as
`2 * keyBytes + 6` (38/54/70 32-bit words). Input key bytes are packed as
big-endian 32-bit words. The 16-byte branch expands words with the classic
RotWord/SubWord/Rcon structure using the client lookup table at image offset
`0x2039730`.

The block transform at `0x7ff6480ae880` reads four big-endian state words,
XORs the first round key, then performs four-table T-table rounds using the
tables at image offsets `0x2039b80`, `0x2039330`, `0x203a7b0`, and
`0x203a380`. This is structurally the Rijndael/AES round function, but the
client tables/key-schedule constants must be compared byte-for-byte with the
standard before replacing it with a framework AES provider. Until that table
comparison is complete the implementation remains client-primitive-specific.


#### AES table comparison

The four client T-tables and key-schedule substitution table were compared
against the canonical AES/Rijndael forward tables. The byte ordering in the
disassembly is big-endian, but after accounting for the DWORD rotations the
table values match the standard AES construction. The key schedule's
RotWord/SubWord/Rcon behavior also matches AES for 128/192/256-bit keys.

This means the block primitive itself can be represented by standard AES block
operations. The earlier OpenSSL control failed because the package constructor
does not use the visible 32-byte ASCII constant directly: it first AES-transforms
both 16-byte halves with the fixed binary preprocessing key, then uses the
resulting 32 bytes as the package key and separately derives the 16-byte tail
mask. Package implementation should reproduce that composition and the client's
block direction/order, rather than porting the large T-tables literally.


### Package crypt validation correction

The first .NET/OpenSSL composition was tested directly against the supplied
`tr4.pkg`. For `script\\abnormalitymode\\abnormalitymodedata.xml`, the first
chunk is flag `0x2`, stored/unpacked length 473 bytes at package offset
`0x20`. Using the proposed fixed-key AES-128 preprocess, AES-256 package key,
AES decrypt for complete blocks, and derived tail mask produced non-XML binary
output. Therefore that composition is **not validated and is disabled in
`PkgEntryReader`**. Flag-`0x2` chunks continue to raise the structured
`PackageCryptRequiredException` instead of emitting corrupt files.

This falsifies at least one assumed direction/input in the constructor chain.
The next implementation must be accepted only when this 473-byte XML entry
(and additional TXT/XML samples) decodes to structurally valid plaintext.


#### Block-direction and validation checkpoint

Client call flow distinguishes the two AES block directions: `0x7ff6480ae880`
is the forward/encrypt block transform and `0x7ff6480aef30` is the inverse/decrypt
block transform. The package seed preprocessing and tail-mask derivation use the
forward transform; flag-`0x2` payload processing reaches the inverse transform
through the decrypt wrapper at `0x7ff64809d990`.

Therefore the failed known-XML control is not explained by simply swapping AES
encrypt/decrypt directions. The active package context used by the reader comes
from the package-crypt singleton and must be traced through its complete
initialization/state path. The first known XML vector has chunk header:
index=0, unpacked=473, stored=473, DWORD3=`0x500AF46B`, flags=`0x2`.
DWORD3 remains deliberately unnamed until its reader-side use is proven.


#### Experimental implementation cleanup

The failed `PackageCrypt.cs` composition has been removed from the project rather
than retained as dead/tempting production code. The extraction path continues to
stop on flag-`0x2` with structured diagnostics. A package crypt implementation
will be reintroduced only after the known 473-byte XML vector passes exactly.


#### Package crypt solved for the active reader

The executable contains two parallel package-crypt constructors. The earlier
`CUMT` path uses ASCII seed `A73FF15DBE914FCC880037FB32E4D314`, but the
reader path matching this package uses the second constructor, whose 32-byte
ASCII seed is `CECA72E7ACDB485C8DF0AC16A2EA6D92`.

Both constructors first AES-128-ECB encrypt the two 16-byte ASCII seed halves
with fixed key `0D68076F0A09076C65730D756E0A650D`. The resulting 32 bytes
form the AES-256 package key. Full payload blocks are AES-256-ECB decrypted;
a final partial block is XORed with AES-256-ECB(key[0..15]).

For the active seed the derived package key is
`1193AE5FB04063423155C655DFDC5D24E9FC6FEF1E5EA691E04976C1D8D5F915`.
This was validated against the first 473-byte flag-2 entry: output begins
`EF BB BF 3C 3F 78 6D 6C` (UTF-8 BOM + `<?xml`) and contains the expected
XML declaration. Package crypt is therefore enabled in `PkgEntryReader`.
