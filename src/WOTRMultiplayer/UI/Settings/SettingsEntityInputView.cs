using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace WOTRMultiplayer.UI.Settings
{
    public class SettingsEntityInputView : SettingsEntityWithValueView<SettingsEntityInputVMBase>
    {
        private GameObject _inputObject;
        private static readonly Color _invalidFormatColor = Color.red;
        private static readonly Color _validFormatColor = new(0.192f, 0.204f, 0.259f, 1.000f);
        private static readonly Color _interactableColor = new(0.196f, 0.208f, 0.271f);
        private static readonly Color _nonInteractableColor = Color.grey;
        private TMP_InputField InputField => _inputObject.GetComponent<TMP_InputField>();

        public override void BindViewImplementation()
        {
            base.BindViewImplementation();

            // restoring references since everything is detached
            m_HighlightedImage = this.gameObject.transform.Find("HighlightedImage").GetComponent<Image>();
            m_Title = this.gameObject.transform.Find("HorizontalLayoutGroup/Text").GetComponent<TextMeshProUGUI>();
            m_MarkImage = this.gameObject.transform.Find("HorizontalLayoutGroup/PointGroup/MarkImage").GetComponent<Image>();
            m_PointImage = this.gameObject.transform.Find("HorizontalLayoutGroup/PointGroup/PointImage").GetComponent<Image>();

            _inputObject = this.gameObject.transform.Find("MultiButton").GetChild(0).gameObject;

            m_Title.text = ViewModel.Title;
            AddDisposable(ViewModel.IsValid.Subscribe(OnIsValidChanged));
            AddDisposable(ViewModel.OnResetTempValue.Subscribe(OnResetTempValue));

            var placeholder = _inputObject.transform.Find(UIFactory.InputPlaceholderObjectName);
            var placeholderText = placeholder.GetComponent<TextMeshProUGUI>();
            placeholderText.SetText(string.Empty);
            placeholderText.alignment = TextAlignmentOptions.Center;

            var input = _inputObject.transform.Find(UIFactory.InputLabelObjectName);
            var inputText = input.GetComponent<TextMeshProUGUI>();
            inputText.overflowMode = TextOverflowModes.Truncate;
            inputText.alignment = TextAlignmentOptions.Center;

            InputField.text = ViewModel.Value;
            InputField.characterLimit = ViewModel.CharacterLimit;
            InputField.onValueChanged.AddListener(ViewModel.OnValueChanged);

            if (!ViewModel.IsModificationAllowed)
            {
                inputText.color = _nonInteractableColor;
                InputField.interactable = false;
            }
            else
            {
                inputText.color = _interactableColor;
                InputField.interactable = true;
            }
        }

        public override void DestroyViewImplementation()
        {
            InputField.onValueChanged.RemoveAllListeners();
        }

        public override void OnModificationChanged(bool allowed)
        {
            ViewModel.ModificationAllowed.Value = allowed;
        }

        private void OnResetTempValue(string newValue)
        {
            InputField.text = newValue;
        }

        private void OnIsValidChanged(bool isValid)
        {
            m_Title.color = isValid ? _validFormatColor : _invalidFormatColor;
        }
    }
}
