using System;
using Kingmaker.Settings;
using Kingmaker.UI;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IKeyboardAccessor
    {
        IDisposable Bind(string key, Action onHotkey);

        void RegisterBinding(string name, KeyBindingData data, KeyboardAccess.GameModesGroup group, bool IsTriggerOnHold, bool isHoldTrigger);
    }
}
