using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Cheats;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.Core.Utils;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UnityBehaviours.DialogAnswers;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class DialogInteractionService : IDialogInteractionService
    {
        public const string SuggestionIconObjectPrefix = "SuggestionIcon";

        private readonly ILogger<DialogInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IUISyncCountersService _uiSyncCountersService;
        private readonly IUIAccessor _uiAccessor;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IResourceProvider _resourceProvider;

        public DialogInteractionService(
            ILogger<DialogInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IUISyncCountersService uiSyncCountersService,
            IResourceProvider resourceProvider,
            IUIAccessor uiAccessor,
            IGameStateLookupService gameStateLookupService)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _uiSyncCountersService = uiSyncCountersService;
            _resourceProvider = resourceProvider;
            _uiAccessor = uiAccessor;
            _gameStateLookupService = gameStateLookupService;
        }

        public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions)
        {
            _mainThreadAccessor.Post(() =>
            {
                ImmediatlyMarkSuggestedDialogAnswers(networkDialogAnswerSuggestions);
            });
        }

        public void ResetSuggestedDialogAnswers()
        {
            ImmediatlyMarkSuggestedDialogAnswers([]);
        }

        public void PlayUnableToSelectCueAnimation(string answerName)
        {
            _mainThreadAccessor.Post(() =>
            {
                var answers = GetAnswers()?.Children() ?? [];
                foreach (var answer in answers)
                {
                    if (string.Equals(answer.gameObject.GetComponent<DialogAnswerPCView>()?.ViewModel?.Answer.Value.name, answerName, StringComparison.OrdinalIgnoreCase))
                    {
                        var selectedAnswerBehavior = answer.gameObject.AddComponent<SelectedDialogAnswerBehavior>();
                        selectedAnswerBehavior.Initialize(duration: 0.8f, onExpired: null);
                        break;
                    }
                }
            });
        }

        public void SelectDialogAnswer(string answerName, string manualUnitSelectionId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var answerBlueprint = Game.Instance.DialogController.Answers.FirstOrDefault(a => string.Equals(a.name, answerName, StringComparison.OrdinalIgnoreCase));
                    if (answerBlueprint == null)
                    {
                        _logger.LogError("Unable to find requested answer. AnswerName={answerName}", answerName);
                        return;
                    }

                    var answers = GetAnswers()?.Children() ?? [];

                    if (!answers.Any())
                    {
                        DoSelectAnswer(answerBlueprint, manualUnitSelectionId);
                        return;
                    }

                    foreach (var answer in answers)
                    {
                        var view = answer.gameObject.GetComponent<DialogAnswerPCView>();
                        if (view == null)
                        {
                            _logger.LogWarning("Answer child has no DialogAnswerPCView component");
                            continue;
                        }

                        if (string.Equals(view.ViewModel.Answer.Value.name, answerName, StringComparison.OrdinalIgnoreCase))
                        {
                            var selectedAnswerBehavior = answer.gameObject.AddComponent<SelectedDialogAnswerBehavior>();
                            selectedAnswerBehavior.Initialize(duration: 0.5f, () => DoSelectAnswer(answerBlueprint, manualUnitSelectionId));
                            continue;
                        }

                        var nonSelectedAnswerBehavior = answer.gameObject.AddComponent<NotSelectedDialogAnswerBehavior>();
                        nonSelectedAnswerBehavior.Initialize(duration: 0.5f, onExpired: null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to select dialog answer. AnswerName={AnswerName}", answerName);
                    throw;
                }
            });
        }

        private void DoSelectAnswer(BlueprintAnswer answerBlueprint, string manualUnitSelectionId)
        {
            ResetSuggestedDialogAnswers();
            var unit = manualUnitSelectionId == null ? null : Game.Instance.Player.PartyAndPets.FirstOrDefault(u => string.Equals(u.UniqueId, manualUnitSelectionId));
            Game.Instance.DialogController.SelectAnswer(answerBlueprint, unit);
        }

        public void SetDialogContinueButtonState(bool isEnabled)
        {
            _mainThreadAccessor.Post(() =>
            {
                const string NextOrEndBindingName = "NextOrEnd";
                try
                {
                    var dialogView = (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_DialogContextPCView;
                    var systemButtonGameObject = dialogView?.m_DialogPCView?.gameObject.transform.Find("Body/SystemButton");
                    var continueButton = systemButtonGameObject?.GetComponent<OwlcatButton>();
                    if (continueButton == null)
                    {
                        _logger.LogWarning("Unable to find system dialog continue button");
                        return;
                    }

                    continueButton.Interactable = isEnabled;
                    bool? hotkeysEnabled = null;
                    if (Game.Instance.Keyboard.m_BindingCallbacks.TryGetValue(NextOrEndBindingName, out var callbacks))
                    {
                        static bool hasConfiguredCallback(Action x) => x.Target is DialogSystemAnswerPCView or UnityEngine.UI.Button.ButtonClickedEvent;

                        if (isEnabled && !callbacks.Any(hasConfiguredCallback))
                        {
                            Game.Instance.Keyboard.Bind(NextOrEndBindingName, continueButton.OnLeftClick.Invoke);
                            hotkeysEnabled = true;
                        }
                        else if (!isEnabled)
                        {
                            callbacks.RemoveAll(hasConfiguredCallback);
                            hotkeysEnabled = false;
                        }
                    }

                    _logger.LogInformation("Dialog continue button updated. IsInteractable={IsInteractable}, HotkeysEnabled={HotkeysEnabled}", isEnabled, hotkeysEnabled);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to change state of system dialog continue button due to error");
                    throw;
                }
            });
        }

        public Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            // this is kinda sketchy, but we need to really know if dialog is already in progress
            // starting dialog is really important as it's required to send `NotifyDialogStarted` to clients
            // unfortunately blueprints can be loaded in mainthread only which means we can't get result right away
            // so it's kinda a workaround so caller (MultiplayerHost) could wait to see if `NotifyDialogStarted` needs to be manually triggered
            var hasStartedDialogTask = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Start dialog. DialogName={DialogName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, MapObjectId={MapObjectId}, SpeakerKey={SpeakerKey}",
                    dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
                var dialogBlueprint = Utilities.GetBlueprint<BlueprintDialog>(dialogName);
                var target = _gameStateLookupService.GetUnitEntity(targetUnitId);
                var initiator = _gameStateLookupService.GetUnitEntity(initiatorUnitId);
                var mapObject = _gameStateLookupService.GetMapObject(mapObjectId);
                var speaker = speakerKey == null ? null : new LocalizedString { Key = speakerKey };
                if (dialogBlueprint == null)
                {
                    _logger.LogError("Unable to find dialog. DialogName={DialogName}", dialogName);
                    return;
                }

                StartDialog(hasStartedDialogTask, dialogBlueprint, initiator, target, mapObject?.View, speaker);
            });

            return hasStartedDialogTask.Task;
        }

        public void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = (Game.Instance.RootUiContext.m_CommonView as CommonPCView).m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update missing dialog popup");
                    return;
                }

                modalMessage.m_AcceptButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AcceptText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Dialog popup UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseDialogPopup(NetworkDialogPopup networkDialogPopup)
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close missing dialog popup. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
                    return;
                }

                modalMessage?.m_AcceptButton.m_OnLeftClick.Invoke();
                _logger.LogInformation("Dialog popup has been closed. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
            });
        }

        private void ImmediatlyMarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
        {
            _logger.LogInformation("Marking dialog answer suggestions. Count={Count}", suggestions.Count);
            if (Game.Instance.DialogController?.Dialog == null)
            {
                _logger.LogWarning("DialogController.Dialog is null");
                return;
            }

            var answers = GetAnswers();
            MarkAnswers(answers, suggestions);

            if (suggestions.Count > 0)
            {
                UISoundController.Instance.Play(UISoundType.GlobalMapRandomEncounter);
            }
        }

        private Transform GetAnswers()
        {
            var dialogContext = _uiAccessor.DialogContextPCView;
            if (dialogContext == null)
            {
                _logger.LogWarning("DialogContextView is null");
                return null;
            }

            switch (Game.Instance.DialogController.Dialog.Type)
            {
                case DialogType.Common:
                    var dialogAnswers = dialogContext.m_DialogPCView.gameObject.transform.Find("Body/View/Scroll View/Viewport/Content/AnswersPanel");
                    return dialogAnswers;
                case DialogType.Book:
                    var bookAnswers = dialogContext.m_BookEventPCView.gameObject.transform.Find("ContentWrapper/Window/Content/Answers");
                    return bookAnswers;
                case DialogType.Interchapter:
                    var interchapterAnswers = dialogContext.m_InterchapterPCView.gameObject.transform.Find("ContentWrapper/Window/Content/Answers");
                    return interchapterAnswers;
                default:
                    _logger.LogWarning("Marking suggested answers has not been implemented for this dialog type. DialogType={DialogType}", Game.Instance.DialogController.Dialog.Type);
                    return null;
            }
        }

        private void MarkAnswers(Transform answersContainer, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            for (int answerIndex = 0; answerIndex < answersContainer.childCount; answerIndex++)
            {
                var answer = answersContainer.GetChild(answerIndex);
                var answerView = answer.GetComponent<DialogAnswerPCView>();
                var answerName = answerView.ViewModel.Answer.Value.name;
                var suggestedAnswer = suggestions.FirstOrDefault(s => string.Equals(s.AnswerName, answerName));

                answer.gameObject.CleanupAllChildren(x => x.name.StartsWith(SuggestionIconObjectPrefix));
                if (suggestedAnswer == null)
                {
                    continue;
                }

                var portrait = _resourceProvider.GetSprite(ResourceBundleProvider.UIBundleName, "UI_Inventory_IconHeart");
                var maxIcons = Math.Min(3, suggestedAnswer.Players.Count);
                for (int i = maxIcons; i > 0; i--)
                {
                    var arrow = answer.Find("Arrow");
                    var suggestionIconObject = UnityEngine.Object.Instantiate(arrow.gameObject, answer);
                    suggestionIconObject.name = SuggestionIconObjectPrefix + i.ToString();
                    suggestionIconObject.SetActive(true);

                    var rect = suggestionIconObject.GetComponent<RectTransform>();
                    var preferedSize = Math.Min(rect.sizeDelta.x, rect.sizeDelta.y);
                    rect.sizeDelta = new Vector2(preferedSize, preferedSize);

                    var newPosition = new Vector3(suggestionIconObject.transform.position.x + 4 - 5 * i, suggestionIconObject.transform.position.y, suggestionIconObject.transform.position.z);
                    suggestionIconObject.transform.SetPositionAndRotation(newPosition, suggestionIconObject.transform.rotation);

                    var image = suggestionIconObject.GetComponent<UnityEngine.UI.Image>();
                    image.color = Color.white;
                    image.sprite = portrait;
                }
            }
        }

        private void StartDialog(TaskCompletionSource<bool> hasStartedDialogTask, BlueprintDialog dialog, UnitEntityData initiator, UnitEntityData target, MapObjectView mapObjectView, LocalizedString customSpeakerName)
        {
            if (string.Equals(Game.Instance.DialogController.Dialog?.name, dialog.name, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Requested dialog already started (most likely due to scripted zone), nothing to do here. DialogName={DialogName}", dialog.name);
                hasStartedDialogTask.SetResult(false);
                return;
            }

            Game.Instance.DialogController.StartDialog(dialog, initiator, target, mapObjectView, customSpeakerName);
            hasStartedDialogTask.SetResult(true);
        }
    }
}
