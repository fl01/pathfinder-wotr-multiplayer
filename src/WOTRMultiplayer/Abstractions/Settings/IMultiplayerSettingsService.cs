using WOTRMultiplayer.Entities.Settings;

namespace WOTRMultiplayer.Abstractions.Settings
{
    public interface IMultiplayerSettingsService
    {
        NetworkMultiplayerSettings GetSettings();

        void Initialize();
    }
}
