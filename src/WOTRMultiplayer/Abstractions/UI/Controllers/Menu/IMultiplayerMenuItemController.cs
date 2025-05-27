using System;
using UnityEngine;

namespace WOTRMultiplayer.Abstractions.UI.Controllers.Menu
{
    public interface IMultiplayerMenuItemController
    {
        void Initialize(IMultiplayerMenuWindow multiplayerWindow, GameObject baseLayout, GameObject menuItem);

        void Activate();
        void Deactivate();

        void Reset();

        Action<object, EventArgs> OnClicked { get; set; }
    }
}
