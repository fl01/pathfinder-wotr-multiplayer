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
            _portraits = _portraits ??= LoadAssets();
        }

        private ConcurrentDictionary<string, UnityEngine.Sprite> LoadAssets()
        {
            var bundle = BundlesLoadService.Instance.RequestBundle("portraits");
            // had no success to limit loading
            // note: you can't delete (Object->Destroy or DestroyImmediate) redundant sprites as it causes texture errors later on
            var allPortraits = bundle.LoadAllAssets<UnityEngine.Sprite>();
            var characterPortraits = new ConcurrentDictionary<string, UnityEngine.Sprite>();
            for (int i = 0; i < allPortraits.Length; i++)
            {
                var portrait = allPortraits[i];

                characterPortraits.TryAdd(portrait.name, portrait);
            }

            return characterPortraits;
        }
    }
}
