using TMPro;
using UnityEngine;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class JoinMenuItemController : MenuItemController
    {
        public const string JoinMenuItemContentObjectName = "JoinMenuItemContent";

        private GameObject _menuContent;

        public override GameObject MenuContent =>  _menuContent;

        public JoinMenuItemController(MultiplayerWindow multiplayerWindow, GameObject menuItem)
            : base(multiplayerWindow, menuItem)
        {
        }

        public override void Activate()
        {
            base.Activate();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = this.MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(StringConsts.MultiplayerWindow.JoinMenuLabel);

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = JoinMenuItemContentObjectName;
            _menuContent.CleanupAllChildren();
        }
    }
}
