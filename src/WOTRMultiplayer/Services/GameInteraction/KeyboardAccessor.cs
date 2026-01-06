using System;
using Kingmaker;
using Kingmaker.Settings;
using Kingmaker.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class KeyboardAccessor : IKeyboardAccessor
    {
        public IDisposable Bind(string key, Action onHotkey)
        {
            return Game.Instance.Keyboard.Bind(key, onHotkey);
        }

        public void RegisterBinding(string name, KeyBindingData data, KeyboardAccess.GameModesGroup gameModes, bool IsTriggerOnHold, bool isHoldTrigger)
        {
            Game.Instance.Keyboard.RegisterBinding(name, data, gameModes, IsTriggerOnHold, isHoldTrigger);
        }
    }
}
