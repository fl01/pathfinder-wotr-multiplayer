using System.IO;
using WOTRMultiplayer.Abstractions.IO;

namespace WOTRMultiplayer.IO
{
    public class FileSystemService : IFileSystemService
    {
        public byte[] GetFileContent(string path)
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
