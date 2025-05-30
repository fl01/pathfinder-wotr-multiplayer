using System.Collections.Concurrent;
using Kingmaker.BundlesLoading;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.UI;

namespace WOTRMultiplayer.UI
{
    public class ResourceLibraryPortraitLoader : IPortraitProvider
    {
        public const string PlaceholderPortrait = "Mask_Portrait";
        private readonly ILogger _logger;
        private ConcurrentDictionary<string, UnityEngine.Sprite> _portraits;

        public ResourceLibraryPortraitLoader(ILogger<ResourceLibraryPortraitLoader> logger)
        {
            _logger = logger;
        }

        public UnityEngine.Sprite GetPortrait(string name)
        {
            if (!_portraits.TryGetValue(name, out var sprite))
            {
                _logger.LogWarning("Unable to find requested portrait. Name={portraitName}", name);
                _portraits.TryGetValue(PlaceholderPortrait, out sprite);
            }

            return sprite;
        }

        public void Initialize()
        {
            _portraits = LoadAssets();
        }

        private ConcurrentDictionary<string, UnityEngine.Sprite> LoadAssets()
        {
            var bundle = BundlesLoadService.Instance.RequestBundle("portraits");
            // had no success to limit loading
            var allPortraits = bundle.LoadAllAssets<UnityEngine.Sprite>();
            var characterPortraits = new ConcurrentDictionary<string, UnityEngine.Sprite>();
            for (int i = 0; i < allPortraits.Length; i++)
            {
                var portrait = allPortraits[i];
                if (!string.IsNullOrEmpty(portrait.name) && portrait.name.EndsWith("Portrait", System.StringComparison.OrdinalIgnoreCase))
                {
                    characterPortraits.TryAdd(portrait.name, portrait);
                    continue;
                }

                // freeup memory
                UnityEngine.Object.DestroyImmediate(portrait);
            }

            return characterPortraits;
        }
    }
}
