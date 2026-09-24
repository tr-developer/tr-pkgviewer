namespace TRPKGViewer.Crypto
{
    public interface ITextDecryptor
    {
        string Version { get; }
        string Name { get; }
        byte[] Decrypt(byte[] data);
    }
}