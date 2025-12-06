using System.Collections.Concurrent;
using Kingmaker.BundlesLoading;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.UI;

namespace WOTRMultiplayer.UI
{
    public class ResourceBundleProvider : IResourceProvider
    {
        public const string PlaceholderPortrait = "Mask_Portrait";
        public const string PortraitsBundleName = "portraits";
        public const string UIBundleName = "ui";

        private readonly ILogger _logger;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Sprite>> _sprites;

        public ResourceBundleProvider(
            ILogger<ResourceBundleProvider> logger)
        {
            _logger = logger;
        }

        public UnityEngine.Sprite GetPortrait(string name)
        {
            _sprites.TryGetValue(PortraitsBundleName, out var portraits);
            if (!portraits.TryGetValue(name, out var sprite))
            {
                _logger.LogWarning("Unable to find requested portrait. PortraitName={PortraitName}", name);
                portraits.TryGetValue(PlaceholderPortrait, out sprite);
            }

            return sprite;
        }

        public UnityEngine.Sprite GetUISprite(string name)
        {
            _sprites.TryGetValue(UIBundleName, out var uiSprites);
            if (!uiSprites.TryGetValue(name, out var sprite))
            {
                _logger.LogWarning("Unable to find requested sprite. SpriteName={SpriteName}", name);
            }

            return sprite;
        }

        public void Initialize()
        {
            if (_sprites == null)
            {
                _sprites = new ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Sprite>>();
                _sprites.TryAdd(PortraitsBundleName, LoadSprites(PortraitsBundleName));
                _sprites.TryAdd(UIBundleName, LoadSprites(UIBundleName));
            }
        }

        private ConcurrentDictionary<string, UnityEngine.Sprite> LoadSprites(string bundleName)
        {
            var bundle = BundlesLoadService.Instance.RequestBundle(bundleName);
            // had no success to limit loading
            // note: you can't delete (Object->Destroy or DestroyImmediate) redundant sprites as it causes texture errors later on
            var allSprites = bundle.LoadAllAssets<UnityEngine.Sprite>();
            var keyValuePairs = new ConcurrentDictionary<string, UnityEngine.Sprite>();
            for (int i = 0; i < allSprites.Length; i++)
            {
                var portrait = allSprites[i];

                keyValuePairs.TryAdd(portrait.name, portrait);
            }

            return keyValuePairs;
        }
    }
}
