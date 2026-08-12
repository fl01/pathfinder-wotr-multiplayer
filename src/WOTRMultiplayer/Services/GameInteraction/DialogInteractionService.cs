using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.Localization;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Kingmaker.UI.MVVM._PCView.InGame;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.Core.Utils;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Graphics;
using WOTRMultiplayer.UnityBehaviours.DialogAnswers;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class DialogInteractionService : IDialogInteractionService
    {
        public const string SuggestionIconObjectPrefix = "SuggestionIcon";

        private readonly ILogger<DialogInteractionService> _logger;
        private readonly IMapper _mapper;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IUISyncCountersService _uiSyncCountersService;
        private readonly IUIAccessor _uiAccessor;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IMultiplayerSettingsService _multiplayerSettingsService;
        private readonly IUIFactory _uiFactory;

        public DialogInteractionService(
            ILogger<DialogInteractionService> logger,
            IMapper mapper,
            IMainThreadAccessor mainThreadAccessor,
            IUISyncCountersService uiSyncCountersService,
            IUIAccessor uiAccessor,
            IUIFactory uiFactory,
            IGameStateLookupService gameStateLookupService,
            IMultiplayerSettingsService multiplayerSettingsService)
        {
            _logger = logger;
            _mapper = mapper;
            _mainThreadAccessor = mainThreadAccessor;
            _uiSyncCountersService = uiSyncCountersService;
            _uiAccessor = uiAccessor;
            _gameStateLookupService = gameStateLookupService;
            _multiplayerSettingsService = multiplayerSettingsService;
            _uiFactory = uiFactory;
        }

        public void MarkSuggestedCueAnswers(List<NetworkPlayer> allPlayers, List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions)
        {
            _mainThreadAccessor.Post(() =>
            {
                MarkDialogAnswers(allPlayers, networkDialogAnswerSuggestions);
            });
        }

        public void ResetSuggestedDialogAnswers()
        {
            MarkDialogAnswers([], []);
        }

        public void PlayUnableToSelectCueAnimation(string answerName)
        {
            _mainThreadAccessor.Post(() =>
            {
                var answers = GetAnswersContainer()?.Children() ?? [];
                var settings = _multiplayerSettingsService.GetSettings();
                foreach (var answer in answers)
                {
                    if (string.Equals(answer.gameObject.GetComponent<DialogAnswerPCView>()?.ViewModel?.Answer.Value.name, answerName, StringComparison.OrdinalIgnoreCase))
                    {
                        var selectedAnswerBehavior = answer.gameObject.AddComponent<SelectedDialogAnswerBehavior>();
                        selectedAnswerBehavior.Begin(settings.DialogBlockedAnswerAnimationDuration, onExpired: null);
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

                    var answers = GetAnswersContainer()?.Children() ?? [];

                    if (!answers.Any())
                    {
                        DoSelectAnswer(answerBlueprint, manualUnitSelectionId);
                        return;
                    }

                    var settings = _multiplayerSettingsService.GetSettings();
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
                            selectedAnswerBehavior.Begin(settings.DialogSelectedAnswerAnimationDuration, () => DoSelectAnswer(answerBlueprint, manualUnitSelectionId));
                            continue;
                        }

                        var nonSelectedAnswerBehavior = answer.gameObject.AddComponent<NotSelectedDialogAnswerBehavior>();
                        nonSelectedAnswerBehavior.Begin(settings.DialogNonSelectedAnswerAnimationDuration, onExpired: null);
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
            var unit = manualUnitSelectionId == null ? null : _gameStateLookupService.GetUnitEntity(manualUnitSelectionId);
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

        public Task<bool> StartDialogAsync(NetworkDialog networkDialog)
        {
            // trying to start a dialog if one is not already in progress
            // clients can't start dialogs on their own unless it's scripted, so this only happens once the dialog has been confirmed by the host
            var hasStartedDialogTask = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Trying to start dialog. Id={Id}, Name={Name}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, MapObjectId={MapObjectId}, SpeakerKey={SpeakerKey}",
                    networkDialog.Id, networkDialog.Name, networkDialog.TargetUnitId, networkDialog.InitiatorUnitId, networkDialog.MapObjectId, networkDialog.SpeakerKey);

                try
                {
                    var dialogBlueprint = ResourcesLibrary.TryGetBlueprint<BlueprintDialog>(networkDialog.Id);
                    if (dialogBlueprint == null)
                    {
                        _logger.LogError("Unable to find dialog. DialogName={DialogName}, DialogId={DialogId}", networkDialog.Name, networkDialog.Id);
                        return;
                    }

                    var target = _gameStateLookupService.GetUnitEntity(networkDialog.TargetUnitId);
                    var initiator = _gameStateLookupService.GetUnitEntity(networkDialog.InitiatorUnitId);
                    var mapObject = _gameStateLookupService.GetMapObject(networkDialog.MapObjectId);
                    var speaker = networkDialog.SpeakerKey == null ? null : new LocalizedString { Key = networkDialog.SpeakerKey };

                    var currentDialog = Game.Instance.DialogController.Dialog;
                    if (currentDialog == null)
                    {
                        _logger.LogInformation("New dialog has been started. DialogName={DialogName}, DialogId={DialogId}", dialogBlueprint.name, dialogBlueprint.AssetGuid.ToString());
                        Game.Instance.DialogController.StartDialog(dialogBlueprint, initiator, target, mapObject?.View, speaker);
                        hasStartedDialogTask.SetResult(true);
                        return;
                    }

                    if (string.Equals(currentDialog.AssetGuid.ToString(), dialogBlueprint.AssetGuid.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Requested dialog already started (most likely due to scripted zone), nothing to do here. DialogName={DialogName}, DialogId={DialogId}", currentDialog.name, currentDialog.AssetGuid.ToString());
                        hasStartedDialogTask.SetResult(false);
                        return;
                    }

                    _logger.LogWarning("Another dialog is already in progress. CurrentDialogName={CurrentDialogName}, CurrentDialogId={CurrentDialogId}, RequestedDialogName={RequestedDialogName}, RequestedDialogId={RequestedDialogId}",
                        currentDialog.name, currentDialog.AssetGuid.ToString(), dialogBlueprint.name, dialogBlueprint.AssetGuid.ToString());
                    hasStartedDialogTask.SetResult(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while starting a dialog");
                    hasStartedDialogTask.SetResult(false);
                    throw;
                }
            });

            return hasStartedDialogTask.Task;
        }

        public void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update missing dialog popup");
                    return;
                }

                modalMessage.m_AcceptButton.Interactable = isInteractable;
                modalMessage.m_DeclineButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AcceptText, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_DeclineText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Dialog popup UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void AcceptDialogPopup(NetworkDialogPopup networkDialogPopup)
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close missing dialog popup. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
                    return;
                }

                modalMessage.m_AcceptButton.Interactable = true;
                modalMessage.m_DeclineButton.Interactable = true;

                modalMessage.m_AcceptButton.m_OnLeftClick.Invoke();
                _logger.LogInformation("Dialog popup has been closed. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
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

                modalMessage?.m_DeclineButton.m_OnLeftClick.Invoke();
                _logger.LogInformation("Dialog popup has been closed. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
            });
        }

        private void MarkDialogAnswers(List<NetworkPlayer> allPlayers, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            _logger.LogInformation("Marking dialog answer suggestions. Count={Count}", suggestions.Count);
            if (Game.Instance.DialogController?.Dialog == null)
            {
                _logger.LogWarning("DialogController.Dialog is null");
                return;
            }

            var answersContainer = GetAnswersContainer();
            if (answersContainer == null)
            {
                return;
            }

            MarkAnswers(answersContainer, allPlayers, suggestions);

            if (suggestions.Count > 0)
            {
                UISoundController.Instance.Play(UISoundType.GlobalMapRandomEncounter);
            }
        }

        private Transform GetAnswersContainer()
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
                case DialogType.Epilogue:
                case DialogType.Interchapter:
                    var interchapterAnswers = dialogContext.m_InterchapterPCView.gameObject.transform.Find("ContentWrapper/Window/Content/Answers");
                    return interchapterAnswers;
                default:
                    _logger.LogWarning("Marking suggested answers has not been implemented for this dialog type. DialogType={DialogType}", Game.Instance.DialogController.Dialog.Type);
                    return null;
            }
        }

        private void MarkAnswers(Transform answersContainer, List<NetworkPlayer> allPlayers, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            const float offset = -12f;
            const float overlap = 7.5f;
            const int maxIconsCount = 4;
            for (int answerIndex = 0; answerIndex < answersContainer.childCount; answerIndex++)
            {
                var answer = answersContainer.GetChild(answerIndex);
                var answerView = answer.GetComponent<DialogAnswerPCView>();
                var answerName = answerView.ViewModel.Answer.Value.name;
                var suggestedAnswer = suggestions.FirstOrDefault(s => string.Equals(s.AnswerName, answerName, StringComparison.OrdinalIgnoreCase));

                var parent = answer.Find("Text");
                parent.gameObject.CleanupAllChildren(x => x.name.StartsWith(SuggestionIconObjectPrefix));
                if (suggestedAnswer == null)
                {
                    continue;
                }

                if (suggestedAnswer.Players.Count == allPlayers.Count && allPlayers.Count > 1)
                {
                    var starObject = new GameObject(SuggestionIconObjectPrefix + "_all");
                    starObject.transform.SetParent(parent, false);
                    var star = starObject.AddComponent<StarIcon>().WithDimensions(points: 8, innerRadius: 7f, outerRadius: 2.5f);
                    star.color = _uiFactory.MuteColor(new Color(0.55f, 0.45f, 0.30f));
                    var rect = starObject.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(14f, 14f);
                    rect.LeftCenter();
                    rect.anchoredPosition = new Vector2(offset, 0f);
                    continue;
                }

                var iconsCount = Math.Min(maxIconsCount, suggestedAnswer.Players.Count);
                for (int i = 0; i < iconsCount; i++)
                {
                    var playerId = suggestedAnswer.Players[i];
                    var playerColor = allPlayers.FirstOrDefault(x => x.Id == playerId)?.Color;
                    if (playerColor == null)
                    {
                        continue;
                    }

                    var color = _mapper.Map<Color>(playerColor);
                    var mutedColor = _uiFactory.MuteColor(color);
                    var suggestionIconObject = _uiFactory.CreateCircleIcon(parent, mutedColor, size: 9f);
                    suggestionIconObject.name = SuggestionIconObjectPrefix + i.ToString();
                    var iconRect = suggestionIconObject.GetComponent<RectTransform>();
                    iconRect.Left();
                    iconRect.anchoredPosition = new Vector2(offset - overlap * i, -0.5f);
                }

                if (suggestedAnswer.Players.Count > maxIconsCount)
                {
                    var moreSignObject = new GameObject(SuggestionIconObjectPrefix + "_more");
                    moreSignObject.transform.SetParent(parent, worldPositionStays: false);
                    var textBox = moreSignObject.AddComponent<TextMeshProUGUI>();
                    textBox.text = "+";
                    textBox.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    textBox.verticalAlignment = VerticalAlignmentOptions.Middle;
                    textBox.fontSize = 12;
                    textBox.color = _uiFactory.MuteColor(Color.red);
                    var rect = moreSignObject.GetComponent<RectTransform>();
                    rect.LeftCenter();
                    rect.anchoredPosition = new Vector2(offset - (overlap - 0.75f) * maxIconsCount, -1.25f);
                    continue;
                }
            }
        }
    }
}
