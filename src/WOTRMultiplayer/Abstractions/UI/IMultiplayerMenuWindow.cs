using System;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;

namespace WOTRMultiplayer.Abstractions.UI
{
    public interface IMultiplayerMenuWindow
    {
        void OnCloseClicked();

        void AssignMenuItemControllers(IHostMenuItemController hostMenuItemController, IJoinMenuItemController joinMenuItemController);

        Action OnDispose { get; set; }
    }
}
