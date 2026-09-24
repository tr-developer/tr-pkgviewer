namespace TRPKGViewer.Crypto
{
    // A decrypt TYPE. Do not put PKG/version selection logic here.
    public interface ITextDecryptor
    {
        string Name { get; }
        byte[] Decrypt(byte[] encryptedData);
    }
}