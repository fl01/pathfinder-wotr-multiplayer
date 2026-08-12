using System.Collections.Generic;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.UI
{
    public static class WellKnownColors
    {
        public static class PlayerColors
        {
            public static NetworkColor Blue { get; } = new NetworkColor(0.20f, 0.48f, 0.75f);

            public static NetworkColor Green { get; } = new NetworkColor(0.30f, 0.62f, 0.35f);

            public static NetworkColor Yellow { get; } = new NetworkColor(0.78f, 0.68f, 0.20f);

            public static NetworkColor SkyBlue { get; } = new NetworkColor(0.35f, 0.70f, 0.90f);

            public static NetworkColor Purple { get; } = new NetworkColor(0.62f, 0.35f, 0.65f);

            public static NetworkColor Red { get; } = new NetworkColor(0.72f, 0.25f, 0.25f);

            public static List<NetworkColor> All { get; private set; }

            static PlayerColors()
            {
                All = [Blue, Green, Yellow, SkyBlue, Purple, Red];
            }
        }

        // 6 colors should be enough, but just in case
        //private static readonly Color[] PlayerColors =
        //[
        //    new(0.20f, 0.48f, 0.75f), // Blue 1
        //    new(0.30f, 0.62f, 0.35f), // Green 1
        //    new(0.78f, 0.68f, 0.20f), // Yellow / Ochre 1
        //    new(0.35f, 0.70f, 0.90f), // Sky blue 1
        //    new(0.62f, 0.35f, 0.65f), // Purple 1
        //    new(0.72f, 0.25f, 0.25f), // Red 1

        //    new(0.35f, 0.35f, 0.35f), // Gray 1
        //    new(0.20f, 0.62f, 0.65f), // Teal 1
        //    new(0.72f, 0.40f, 0.52f), // Pink 1
        //    new(0.38f, 0.42f, 0.68f), // Indigo 1
        //    new(0.00f, 0.45f, 0.70f), // Blue 2
        //    new(0.90f, 0.60f, 0.00f), // Orange
        //    new(0.00f, 0.60f, 0.50f), // Teal 2
        //    new(0.80f, 0.30f, 0.20f), // Vermillion
        //    new(0.95f, 0.90f, 0.25f), // Yellow 2
        //    new(0.80f, 0.45f, 0.70f), // Purple 2
        //    new(0.52f, 0.65f, 0.22f), // Lime 1
        //    new(0.55f, 0.35f, 0.65f), // Violet
        //    new(0.55f, 0.55f, 0.25f), // Olive
        //    new(0.80f, 0.38f, 0.16f), // Orange 1
        //];
    }
}
