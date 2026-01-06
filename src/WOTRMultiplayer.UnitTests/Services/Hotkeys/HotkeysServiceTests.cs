using System;
using FakeItEasy;
using Kingmaker.Settings;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Services.Hotkeys;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.UnitTests.Services.Hotkeys
{
    [TestFixture]
    public class HotkeysServiceTests
    {
        private HotkeysService _hotkeysService;
        private ILogger<HotkeysService> _logger;
        private IMultiplayerActorAccessor _multiplayerActorAccessor;
        private ISettingsControllerAccessor _settingsControllerAccessor;
        private IKeyboardAccessor _keyboardAccessor;

        [SetUp]
        public void SetUp()
        {
            WellKnownSettings.Initialize();

            _logger = A.Fake<ILogger<HotkeysService>>();
            _multiplayerActorAccessor = A.Fake<IMultiplayerActorAccessor>();
            _settingsControllerAccessor = A.Fake<ISettingsControllerAccessor>();
            _keyboardAccessor = A.Fake<IKeyboardAccessor>();

            _hotkeysService = new HotkeysService(
                _logger,
                _multiplayerActorAccessor,
                _settingsControllerAccessor,
                _keyboardAccessor);
        }

        [Test]
        public void ConfigureHotkey_NotNullSetting_ConfiguresCallback()
        {
            // Arrange
            var setting = new WellKnownSettingKey<KeyBindingPair>(default);

            // Act
            _hotkeysService.ConfigureHotkey(setting, null);

            // Assert
            A.CallTo(() => _keyboardAccessor.Bind(setting.Key, A<Action>.Ignored)).MustHaveHappened();
        }

        [Test]
        public void Callback_ConfiguredSettingButMultiplayerIsNotActive_CallbackIsSkipped()
        {
            // Arrange
            A.CallTo(() => _multiplayerActorAccessor.Current).Returns(null);
            var setting = WellKnownSettings.Hotkeys.Ping;
            var hasBeenCalled = false;
            void hotkeyHandler() { hasBeenCalled = true; }
            _hotkeysService.ConfigureHotkey(setting, hotkeyHandler);
            _hotkeysService.Initialize();
            var callback = FakeUtils.GetHotkeyCallback(setting, _keyboardAccessor);

            // Act
            callback.Invoke();

            // Assert
            Assert.That(hasBeenCalled, Is.False);
        }

        [Test]
        public void Callback_ConfiguredSettingAndMultiplayerIsActive_CallbackIsCalled()
        {
            // Arrange
            var setting = WellKnownSettings.Hotkeys.Ping;
            var hasBeenCalled = false;
            void hotkeyHandler() { hasBeenCalled = true; }
            _hotkeysService.ConfigureHotkey(setting, hotkeyHandler);
            _hotkeysService.Initialize();
            var callback = FakeUtils.GetHotkeyCallback(setting, _keyboardAccessor);

            // Act
            callback.Invoke();

            // Assert
            Assert.That(hasBeenCalled, Is.True);
        }

        [Test]
        public void ConfigureHotkey_NoBindingPairs_BindingsAreNotRegistered()
        {
            // Arrange
            var setting = new WellKnownSettingKey<KeyBindingPair>(default) { Key = "no-bindings" };
            var actualValue = new KeyBindingPair { TriggerOnHold = true };
            A.CallTo(() => _settingsControllerAccessor.GetValue(setting)).Returns(actualValue);

            // Act
            _hotkeysService.ConfigureHotkey(setting, null);

            // Assert
            A.CallTo(() => _keyboardAccessor.RegisterBinding(setting.Key, actualValue.Binding1, A<KeyboardAccess.GameModesGroup>.Ignored, A<bool>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => _keyboardAccessor.RegisterBinding(setting.Key, actualValue.Binding2, A<KeyboardAccess.GameModesGroup>.Ignored, A<bool>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
        }

        [Test]
        public void ConfigureHotkey_FirstBindingPairOnly_ConfiguresFirstBinding()
        {
            // Arrange
            var setting = new WellKnownSettingKey<KeyBindingPair>(default) { Key = "first-binding" };
            var actualValue = new KeyBindingPair { Binding1 = new KeyBindingData { Key = UnityEngine.KeyCode.Q }, TriggerOnHold = true };
            A.CallTo(() => _settingsControllerAccessor.GetValue(setting)).Returns(actualValue);

            // Act
            _hotkeysService.ConfigureHotkey(setting, null);

            // Assert
            A.CallTo(() => _keyboardAccessor.RegisterBinding(setting.Key, actualValue.Binding1, actualValue.GameModesGroup, actualValue.TriggerOnHold, false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _keyboardAccessor.RegisterBinding(setting.Key, actualValue.Binding2, A<KeyboardAccess.GameModesGroup>.Ignored, A<bool>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
        }

        [Test]
        public void ConfigureHotkey_SecondBindingPairOnly_ConfiguresSecondBinding()
        {
            // Arrange
            var setting = new WellKnownSettingKey<KeyBindingPair>(default) { Key = "second-binding" };
            var actualValue = new KeyBindingPair { Binding2 = new KeyBindingData { Key = UnityEngine.KeyCode.Q }, TriggerOnHold = true };
            A.CallTo(() => _settingsControllerAccessor.GetValue(setting)).Returns(actualValue);

            // Act
            _hotkeysService.ConfigureHotkey(setting, null);

            // Assert
            A.CallTo(() => _keyboardAccessor.RegisterBinding(setting.Key, actualValue.Binding2, actualValue.GameModesGroup, actualValue.TriggerOnHold, false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _keyboardAccessor.RegisterBinding(setting.Key, actualValue.Binding1, A<KeyboardAccess.GameModesGroup>.Ignored, A<bool>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
        }
    }
}
