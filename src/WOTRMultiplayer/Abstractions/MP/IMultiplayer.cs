using Kingmaker.UI.MVVM._PCView.EscMenu;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayer
    {
        IUIFactory Factory { get; }

        bool InitializeMultiplayer(GameObject menuButtonToCopy, Transform parent);

        void TerminateMultiplayer();

        void CreateEscMenuItem(EscMenuPCView view);

        bool IsActive { get; }
    }
}
