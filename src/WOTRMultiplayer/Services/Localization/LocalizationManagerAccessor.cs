using System.Collections.Generic;
using Kingmaker.Localization;
using WOTRMultiplayer.Abstractions.Localization;

namespace WOTRMultiplayer.Services.Localization
{
    public class LocalizationManagerAccessor : ILocalizationManagerAccessor
    {
        public void UpdateCurrentLocalePack(Dictionary<string, string> translations)
        {
            foreach (var kv in translations)
            {
                LocalizationManager.CurrentPack.PutString(kv.Key, kv.Value);
            }
        }
    }
}
