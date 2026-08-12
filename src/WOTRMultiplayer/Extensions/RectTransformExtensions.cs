using UnityEngine;

namespace WOTRMultiplayer.Extensions
{
    public static class RectTransformExtensions
    {
        public static void Center(this RectTransform transform)
        {
            transform.pivot = new Vector2(0.5f, 0.5f);
            transform.anchorMin = new Vector2(0.5f, 0.5f);
            transform.anchorMax = new Vector2(0.5f, 0.5f);
        }

        public static void Left(this RectTransform transform)
        {
            transform.anchorMin = new Vector2(0f, 0.5f);
            transform.anchorMax = new Vector2(0f, 0.5f);
            transform.pivot = new Vector2(0f, 0.5f);
        }

        public static void LeftCenter(this RectTransform transform)
        {
            transform.anchorMin = new Vector2(0f, 0.5f);
            transform.anchorMax = new Vector2(0f, 0.5f);
            transform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
