using System.IO;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.IO;

namespace WOTRMultiplayer.IO
{
    public class FileSystemService : IFileSystemService
    {
        private readonly ILogger<FileSystemService> _logger;

        public FileSystemService(ILogger<FileSystemService> logger)
        {
            _logger = logger;
        }

        public byte[] GetFile(string path)
        {
            _logger.LogInformation("Retrieving file. Path={path}", path);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public bool WriteFile(string path, byte[] content)
        {
            _logger.LogInformation("Writing file content. Path={path}, ContentSize={contentSize}", path, content.Length);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, content);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to write file");
                return false;
            }
        }
    }
}
