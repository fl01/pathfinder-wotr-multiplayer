namespace WOTRMultiplayer.Abstractions.IO
{
    public interface IFileSystemService
    {
        byte[] GetRawFileContent(string path);

        string GetFileContent(string path);

        bool WriteFile(string path, byte[] content);

        bool WriteFile(string path, string content);
    }
}
