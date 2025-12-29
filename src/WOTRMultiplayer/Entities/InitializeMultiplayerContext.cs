using UnityEngine;

namespace WOTRMultiplayer.Entities
{
    /// <summary>
    /// simple workaround to be able to mock unity related stuff
    /// </summary>
    public class InitializeMultiplayerContext
    {
        public virtual GameObject MenuItemPrototype { get; private set; }

        public virtual Transform Parent { get; private set; }

        public InitializeMultiplayerContext(GameObject menuItemPrototype, Transform parent)
        {
            MenuItemPrototype = menuItemPrototype;
            Parent = parent;
        }
    }
}
