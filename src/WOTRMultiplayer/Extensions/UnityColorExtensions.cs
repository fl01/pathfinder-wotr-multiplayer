using UnityEngine;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Extensions
{
    public static class UnityColorExtensions
    {
        public static Color ToUnityColor(this NetworkColor networkColor)
        {
            if (networkColor == null)
            {
                return default;
            }

            var color = new Color(networkColor.R, networkColor.G, networkColor.B, networkColor.A);
            return color;
        }
    }
}
