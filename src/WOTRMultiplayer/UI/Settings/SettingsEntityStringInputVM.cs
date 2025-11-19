using WOTRMultiplayer.UI.Settings.Entities;

namespace WOTRMultiplayer.UI.Settings
{
    public class SettingsEntityStringInputVM : SettingsEntityInputVMBase
    {
        private readonly UIValidatableSettingsEntityBase<string> _settingEntity;

        public SettingsEntityStringInputVM(UIValidatableSettingsEntityBase<string> settingEntity)
            : base(settingEntity, settingEntity.CharacterLimit)
        {
            _settingEntity = settingEntity;
        }

        public override void OnValueChanged(string value)
        {
            _settingEntity.SetTempValue(value);
            IsValid.Value = _settingEntity.Validator?.Validate(value).IsValid ?? true;
        }
    }
}
