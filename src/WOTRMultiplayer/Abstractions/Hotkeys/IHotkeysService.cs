using System;
using Kingmaker.Settings;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.Abstractions.Hotkeys
{
    public interface IHotkeysService
    {
        void Initialize();

        void ConfigureHotkey(WellKnownSettingKey<KeyBindingPair> hotkey, Action hotkeyHandler);
    }
}
