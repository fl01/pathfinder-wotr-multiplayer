using System;
using UnityEngine;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.Abstractions.UI.Controllers.Menu
{
    public interface IMultiplayerMenuItemController
    {
        ModalActionConfirmation GetDeactivationConfirmation();

        void Initialize(IMultiplayerMenuWindow multiplayerWindow, GameObject baseLayout, GameObject menuItem);

        void Activate();
        void Deactivate();

        void Reset(bool isSoftReset);

        bool IsActive { get; }
        Action<object, EventArgs> OnClicked { get; set; }
    }
}
