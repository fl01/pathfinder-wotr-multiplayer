using UnityEngine.UI.Extensions;
using WOTRMultiplayer.Abstractions.Unity;

namespace WOTRMultiplayer.Unity
{
    public class UnityPathService : IUnityPathService
    {
        public string GetSaveGamePath()
        {
            return SaveLoad.saveGamePath;
        }
    }
}
