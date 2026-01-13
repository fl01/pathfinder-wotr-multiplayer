using System;
using System.Collections.Concurrent;
using Kingmaker.BundlesLoading;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.UI;

namespace WOTRMultiplayer.UI
{
    public class ResourceBundleProvider : IResourceProvider
    {
        private readonly ILogger _logger;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Sprite>> _sprites;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Texture2D>> _textures;

        public ResourceBundleProvider(
            ILogger<ResourceBundleProvider> logger)
        {
            _logger = logger;
        }

        public UnityEngine.Sprite GetSprite(string bundleName, string spriteName)
        {
            _sprites.TryGetValue(bundleName, out var spriteBundle);
            UnityEngine.Sprite sprite = null;
            if (spriteBundle == null || !spriteBundle.TryGetValue(spriteName, out sprite))
            {
                _logger.LogWarning("Unable to find requested sprite. BundleName={BundleName}, SpriteName={SpriteName}", bundleName, spriteName);
            }

            return sprite;
        }
        public UnityEngine.Texture2D GetTexture2D(string bundleName, string textureName)
        {
            _textures.TryGetValue(bundleName, out var textureBundle);
            UnityEngine.Texture2D texture = null;
            if (textureBundle == null || !textureBundle.TryGetValue(textureName, out texture))
            {
                _logger.LogWarning("Unable to find requested texture. TextureName={TextureName}, BundleName={BundleName}", bundleName, textureName);
            }

            return texture;
        }

        public void Initialize()
        {
            if (_sprites == null)
            {
                _sprites = new ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Sprite>>();
                _sprites.TryAdd(WellKnownSpriteBundles.Portraits, LoadBundle<UnityEngine.Sprite>(WellKnownSpriteBundles.Portraits));
                _sprites.TryAdd(WellKnownSpriteBundles.UI, LoadBundle<UnityEngine.Sprite>(WellKnownSpriteBundles.UI));
            }

            if (_textures == null)
            {
                _textures = new ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Texture2D>>();
                _textures.TryAdd(WellKnownSpriteBundles.UI, LoadBundle<UnityEngine.Texture2D>(WellKnownSpriteBundles.UI));
            }
        }

        private ConcurrentDictionary<string, T> LoadBundle<T>(string bundleName)
            where T : UnityEngine.Object
        {
            var bundle = BundlesLoadService.Instance.RequestBundle(bundleName);
            var allSprites = bundle.LoadAllAssets<T>();
            var keyValuePairs = new ConcurrentDictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < allSprites.Length; i++)
            {
                var portrait = allSprites[i];

                keyValuePairs.TryAdd(portrait.name, portrait as T);
            }

            return keyValuePairs;
        }
    }
}
