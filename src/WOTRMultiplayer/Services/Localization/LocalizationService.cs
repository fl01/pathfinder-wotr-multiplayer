using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Localization;

namespace WOTRMultiplayer.Services.Localization
{
    public class LocalizationService : ILocalizationService
    {
        public const string LocalizationFolderName = "Localization";
        public const string FallbackLocale = "enGB";

        private readonly ILogger<LocalizationService> _logger;
        private readonly IFileSystemService _fileSystemService;
        private readonly ILocalizationManagerAccessor _localizationManagerAccessor;

        public LocalizationService(
            ILogger<LocalizationService> logger,
            IFileSystemService fileSystemService,
            ILocalizationManagerAccessor localizationManagerAccessor)
        {
            _logger = logger;
            _fileSystemService = fileSystemService;
            _localizationManagerAccessor = localizationManagerAccessor;
        }

        public void UpdateLocale(string locale)
        {
            var localeFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), LocalizationFolderName);

            var pack = GetLocalePack(FallbackLocale, localeFolder);
            if (pack == null)
            {
                _logger.LogError("Base locale pack is not available. Locale={Locale} LocalePath={LocalePath}", FallbackLocale, localeFolder);
                return;
            }

            if (!string.Equals(locale, FallbackLocale, StringComparison.OrdinalIgnoreCase))
            {
                var targetPack = GetLocalePack(locale, localeFolder);
                if (targetPack == null)
                {
                    _logger.LogWarning("Requested locale pack is not available. Locale={Locale} LocalePath={LocalePath}", FallbackLocale, localeFolder);
                }
                else
                {
                    pack = MergeLocalePacks(pack, targetPack);
                    _logger.LogInformation("Locale pack has been merged. BaseLocale={BaseLocale}, TargetLocale={TargetLocale}", FallbackLocale, locale);
                }
            }

            _localizationManagerAccessor.UpdateCurrentLocalePack(pack);
            _logger.LogInformation("Locale has been configured. Locale={Locale}", locale);
        }

        public Dictionary<string, string> MergeLocalePacks(Dictionary<string, string> baseLocale, Dictionary<string, string> targetLocale)
        {
            var mergedLocale = new Dictionary<string, string>();
            foreach (var kv in targetLocale)
            {
                mergedLocale.Add(kv.Key, kv.Value);
            }

            foreach (var kv in baseLocale)
            {
                if (mergedLocale.ContainsKey(kv.Key))
                {
                    continue;
                }

                mergedLocale.Add(kv.Key, kv.Value);
            }

            return mergedLocale;
        }

        private Dictionary<string, string> GetLocalePack(string localeName, string localeFolder)
        {
            var rawPack = GetLocaleContent(localeName, localeFolder);
            if (rawPack == null)
            {
                _logger.LogError("Locale pack is missing. Locale={Locale} LocalePath={LocalePath}", localeName, localeFolder);
                return null;
            }

            var pack = ParsePack(rawPack);
            if (pack == null || pack.Keys.Count == 0)
            {
                _logger.LogError("Locale pack is corrupted. Locale={Locale} LocalePath={LocalePath}", localeName, localeFolder);
                return null;
            }

            return pack;
        }

        private Dictionary<string, string> ParsePack(string json)
        {
            try
            {
                var rawLocalePack = JsonConvert.DeserializeObject<JObject>(json);
                var tokensToProcess = new Stack<JToken>();
                tokensToProcess.Push(rawLocalePack.Root);
                var pack = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (tokensToProcess.Count > 0)
                {
                    var current = tokensToProcess.Pop();
                    var children = current.Children();
                    if (children.Any())
                    {
                        foreach (var child in children)
                        {
                            tokensToProcess.Push(child);
                        }
                        continue;
                    }

                    var key = current.Path;
                    var value = current.ToString();
                    if (pack.ContainsKey(key))
                    {
                        _logger.LogWarning("Duplicate locale key detected. It will be ignored. Key={Key}, Value={Value}", key, value);
                        continue;
                    }

                    pack.Add(key, value);
                }

                return pack;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to parse locale pack");
                return null;
            }
        }

        private string GetLocaleContent(string localeName, string localeFolder)
        {
            var targetlocalePackPath = Path.Combine(localeFolder, $"{localeName}.json");
            var content = _fileSystemService.GetFileContent(targetlocalePackPath);
            if (content == null)
            {
                return null;
            }

            return content;
        }

        private class LocalePack
        {
            public string Name { get; set; }

            public byte[] Content { get; set; }
        }
    }
}
