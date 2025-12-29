using Kingmaker.UI.MVVM._PCView.EscMenu;

namespace WOTRMultiplayer.Entities
{
    public class InitializeEscMenuLobbyWindowContext
    {
        public EscMenuPCView View { get; private set; }

        public InitializeEscMenuLobbyWindowContext(EscMenuPCView view)
        {
            View = view;
        }
    }
}
