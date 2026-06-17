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
                _sprites.TryAdd(WellKnownResourceBundles.Portraits, LoadBundle<UnityEngine.Sprite>(WellKnownResourceBundles.Portraits));
                _sprites.TryAdd(WellKnownResourceBundles.UI, LoadBundle<UnityEngine.Sprite>(WellKnownResourceBundles.UI));
                _sprites.TryAdd(WellKnownResourceBundles.Icons, LoadBundle<UnityEngine.Sprite>(WellKnownResourceBundles.Icons));

                var spriteContainer = _sprites[WellKnownResourceBundles.Icons];
                LoadSpriteAtlases(WellKnownResourceBundles.Icons, spriteContainer);
            }

            if (_textures == null)
            {
                _textures = new ConcurrentDictionary<string, ConcurrentDictionary<string, UnityEngine.Texture2D>>();
                _textures.TryAdd(WellKnownResourceBundles.UI, LoadBundle<UnityEngine.Texture2D>(WellKnownResourceBundles.UI));
            }
        }

        private void LoadSpriteAtlases(string bundleName, ConcurrentDictionary<string, UnityEngine.Sprite> container)
        {
            var bundle = BundlesLoadService.Instance.RequestBundle(bundleName);
            var atlases = bundle.LoadAllAssets<UnityEngine.U2D.SpriteAtlas>();
            foreach (var atlas in atlases)
            {
                var sprites = new UnityEngine.Sprite[atlas.spriteCount];
                atlas.GetSprites(sprites);
                foreach (var sprite in sprites)
                {
                    var name = sprite.name.Trim("(Clone)").ToString();
                    container.TryAdd(name, sprite);
                }
            }
        }

        private ConcurrentDictionary<string, T> LoadBundle<T>(string bundleName)
            where T : UnityEngine.Object
        {
            var bundle = BundlesLoadService.Instance.RequestBundle(bundleName);
            var allResources = bundle.LoadAllAssets<T>();
            var keyValuePairs = new ConcurrentDictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < allResources.Length; i++)
            {
                var resource = allResources[i];
                keyValuePairs.TryAdd(resource.name, resource);
            }

            return keyValuePairs;
        }
    }
}
