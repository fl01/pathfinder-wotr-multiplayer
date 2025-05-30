namespace WOTRMultiplayer.Abstractions.IO
{
    public interface IFileSystemService
    {
        byte[] GetFile(string path);

        bool WriteFile(string path, byte[] content);
    }
}
