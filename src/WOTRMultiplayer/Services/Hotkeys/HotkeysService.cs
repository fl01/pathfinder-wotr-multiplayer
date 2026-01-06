using System;
using System.Collections.Generic;
using Kingmaker.Settings;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hotkeys;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.Services.Hotkeys
{
    public class HotkeysService : IHotkeysService
    {
        private readonly ILogger<HotkeysService> _logger;
        private readonly IMultiplayerActorAccessor _multiplayerActorAccessor;
        private readonly ISettingsControllerAccessor _settingsControllerAccessor;
        private readonly IKeyboardAccessor _keyboardAccessor;

        private readonly List<IDisposable> _bindings = [];
        public HotkeysService(
            ILogger<HotkeysService> logger,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            ISettingsControllerAccessor settingsControllerAccessor,
            IKeyboardAccessor keyboardAccessor)
        {
            _logger = logger;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _settingsControllerAccessor = settingsControllerAccessor;
            _keyboardAccessor = keyboardAccessor;
        }

        public void Initialize()
        {
            ConfigureHotkey(WellKnownSettings.Hotkeys.Ping, OnPingHotkey);
        }

        public void ConfigureHotkey(WellKnownSettingKey<KeyBindingPair> hotkey, Action hotkeyHandler)
        {
            try
            {
                var actualValue = _settingsControllerAccessor.GetValue(hotkey);
                if (actualValue.Binding1.Key != KeyCode.None)
                {
                    _keyboardAccessor.RegisterBinding(hotkey.Key, actualValue.Binding1, actualValue.GameModesGroup, actualValue.TriggerOnHold, isHoldTrigger: false);
                }
                if (actualValue.Binding2.Key != KeyCode.None)
                {
                    _keyboardAccessor.RegisterBinding(hotkey.Key, actualValue.Binding2, actualValue.GameModesGroup, actualValue.TriggerOnHold, isHoldTrigger: false);
                }

                var bind = _keyboardAccessor.Bind(hotkey.Key, () => OnHotkeyPressed(hotkey.Key, hotkeyHandler));
                _bindings.Add(bind);
                _logger.LogInformation("Hotkey has been configured. Key={Key}", hotkey.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to configure hotkey setting. Key={Key}", hotkey?.Key);
                throw;
            }
        }

        private void OnHotkeyPressed(string key, Action onHotkey)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            try
            {
                onHotkey.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing hotkey handler. Key={Key}", key);
            }
        }

        private void OnPingHotkey()
        {
            _logger.LogWarning("Ping hotkey pressed");
        }
    }
}
