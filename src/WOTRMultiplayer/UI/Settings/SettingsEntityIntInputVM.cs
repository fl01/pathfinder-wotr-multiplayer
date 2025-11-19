using WOTRMultiplayer.UI.Settings.Entities;

namespace WOTRMultiplayer.UI.Settings
{
    public class SettingsEntityIntInputVM : SettingsEntityInputVMBase
    {
        private readonly UIValidatableSettingsEntityBase<int> _settingEntity;

        public SettingsEntityIntInputVM(UIValidatableSettingsEntityBase<int> settingEntity)
            : base(settingEntity, settingEntity.CharacterLimit)
        {
            _settingEntity = settingEntity;
        }

        public override void OnValueChanged(string value)
        {
            if (!int.TryParse(value?.Trim(), out var intValue))
            {
                IsValid.Value = false;
                return;
            }

            _settingEntity.SetTempValue(intValue);
            IsValid.Value = _settingEntity.Validator?.Validate(intValue).IsValid ?? true;
        }
    }
}
