namespace WOTRMultiplayer.Abstractions.IO
{
    public interface IFileSystemService
    {
        byte[] GetFileContent(string path);
    }
}
