using UnityEngine;

namespace WOTRMultiplayer.Extensions
{
    public static class RectTransformExtensions
    {
        public static void Centered(this RectTransform transform)
        {
            transform.pivot = new Vector2(0.5f, 0.5f);
            transform.anchorMin = new Vector2(0.5f, 0.5f);
            transform.anchorMax = new Vector2(0.5f, 0.5f);
        }
    }
}
