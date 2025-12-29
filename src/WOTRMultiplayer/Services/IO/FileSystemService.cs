using System.IO;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.IO;

namespace WOTRMultiplayer.Services.IO
{
    public class FileSystemService : IFileSystemService
    {
        private readonly ILogger<FileSystemService> _logger;

        public FileSystemService(ILogger<FileSystemService> logger)
        {
            _logger = logger;
        }

        public byte[] GetRawFileContent(string path)
        {
            _logger.LogInformation("Retrieving file as byte[]. Path={Path}", path);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public string GetFileContent(string path)
        {
            _logger.LogInformation("Retrieving file as string. Path={Path}", path);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public bool WriteFile(string path, byte[] content)
        {
            _logger.LogInformation("Writing file content. Path={Path}, ContentSize={ContentSize}", path, content.Length);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, content);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to write file. Path={Path}", path);
                return false;
            }
        }
    }
}
