using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UI.MVVM._VM.Dialog.Dialog;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.GameInteraction
{
    public class GameInteractionService : IGameInteractionService
    {
        private readonly ILogger<GameInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IPortraitProvider _portraitProvider;

        public GameInteractionService(
            ILogger<GameInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IPortraitProvider portraitProvider)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _portraitProvider = portraitProvider;
        }

        public bool IsPaused => Game.Instance.IsPaused;

        public void LeaveArea(string areaExitId)
        {
            _logger.LogInformation("Leaving area. AreaExitId={areaExitId}", areaExitId);
            _mainThreadAccessor.Enqueue(() =>
            {
                var allTransitions = Game.Instance.State.MapObjects.All.Select(o => o.View.GetComponent<AreaTransition>()).Where(t => t != null).ToList();
                var transition = allTransitions.FirstOrDefault(x => string.Equals(x.GetComponent<MapObjectView>().UniqueId, areaExitId, System.StringComparison.OrdinalIgnoreCase));
                var areaTransition = transition?.GetComponent<MapObjectView>()?.Data.Get<AreaTransitionPart>();
                if (areaTransition == null)
                {
                    _logger.LogError("Unable to find requested area transition. TransitionsCount={transitionsCount}, AreaExitId={areaExitId}", allTransitions.Count, areaExitId);
                    return;
                }

                // AreaTransitionGroupCommand.ExecuteTransition
                if (Game.Instance.State.LoadedAreaState.Encounter == null && areaTransition.AreaEnterPoint.Area.IsGlobalMap)
                {
                    BlueprintGlobalMap globalMap = BlueprintRoot.Instance.GlobalMap.GetGlobalMap(areaTransition.AreaEnterPoint);
                    if (globalMap != null)
                    {
                        Game.Instance.Player.GetGlobalMap(globalMap).Player.AreaReturnPoint = areaTransition.GetEnterPointToReturnTo();
                    }
                }

                Game.Instance.LoadArea(areaTransition.AreaEnterPoint, areaTransition.Settings.AutoSaveMode, null);
            });

        }

        public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
        {
            const string SuggestionIconName = "SuggestionIcon";
            _mainThreadAccessor.Enqueue(() =>
            {
                var dialogView = (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_DialogContextPCView?.m_DialogPCView;
                var gameObject = dialogView.gameObject;
                var answers = gameObject.transform.Find("Body/View/Scroll View/Viewport/Content/AnswersPanel");
                for (int answerIndex = 0; answerIndex < answers.childCount; answerIndex++)
                {
                    var answer = answers.GetChild(answerIndex);
                    var answerView = answer.GetComponent<DialogAnswerPCView>();
                    var answerName = (answerView.GetViewModel() as AnswerVM).Answer.Value.name;
                    var suggestedAnswer = suggestions.FirstOrDefault(s => string.Equals(s.AnswerName, answerName));
                    var existingSuggestionObject = answer.Find(SuggestionIconName)?.gameObject;
                    if (suggestedAnswer != null)
                    {
                        if (existingSuggestionObject == null)
                        {
                            var arrow = answer.Find("Arrow");
                            existingSuggestionObject = UnityEngine.Object.Instantiate(arrow.gameObject, answer);
                            existingSuggestionObject.name = SuggestionIconName;
                            existingSuggestionObject.SetActive(true);
                        }

                        var image = existingSuggestionObject.GetComponent<Image>();
                        var portrait = _portraitProvider.GetPortrait("Mask_Portrait");
                        image.sprite = portrait;
                        PlaySound(UISoundType.GlobalMapRandomEncounter);
                        _logger.LogInformation("Created answer suggestion icon. AnswerIndex={answerIndex}, AnswerName={answerName}", answerIndex, suggestedAnswer.AnswerName);
                        return;
                    }

                    if (existingSuggestionObject != null)
                    {
                        _logger.LogInformation("Deleted answer suggestion icon. AnswerIndex={answerIndex}", answerIndex);
                        UnityEngine.Object.DestroyImmediate(existingSuggestionObject);
                    }
                }
            });
        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            var character = Game.Instance.Player.PartyAndPets.FirstOrDefault(f => string.Equals(f.CharacterName, characterName));
            if (character == null)
            {
                _logger.LogError("Can't find character. Name={characterName}", characterName);
                return;
            }

            var unityDestination = new UnityEngine.Vector3(destination.X, destination.Y, destination.Z);
            var command = new UnitMoveTo(unityDestination, 0.3f)
            {
                MovementDelay = delay,
                Orientation = orientation,
                CreatedByPlayer = true
            };
            character.Commands.Run(command);
        }

        public void Pause(bool isPaused)
        {
            _logger.LogInformation("Pause game. Value={isPaused}", isPaused);
            if (isPaused)
            {
                Game.Instance.StartMode(GameModeType.Pause);
                return;
            }

            Game.Instance.StopMode(GameModeType.Pause);
        }

        public void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId)
        {
            var answer = Game.Instance.DialogController.Answers.FirstOrDefault(a => string.Equals(a.name, answerName, StringComparison.OrdinalIgnoreCase));
            if (answer == null)
            {
                _logger.LogError("Unable to find requested answer. AnswerName={answerName}", answerName);
                return;
            }

            var unit = manualUnitSelectionId == null ? null : Game.Instance.Player.PartyAndPets.FirstOrDefault(u => string.Equals(u.UniqueId, manualUnitSelectionId));
            Game.Instance.DialogController.SelectAnswer(answer, unit);
        }

        public void SetDialogContinueButtonState(bool isEnabled)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                const string NextOrEndBindingName = "NextOrEnd";
                try
                {
                    var dialogView = (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_DialogContextPCView;
                    var systemButtonGameObject = dialogView?.m_DialogPCView?.gameObject.transform.Find("Body/SystemButton");
                    var continueButton = systemButtonGameObject?.GetComponent<OwlcatButton>();
                    if (continueButton == null)
                    {
                        _logger.LogError("Unable to find system dialog continue button");
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

                    _logger.LogInformation("Continue button updated. IsInteractable={isInteractable}, HotkeysEnabled={hotkeysEnabled}", isEnabled, hotkeysEnabled);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to change state of system dialog continue button due to error");
                    throw;
                }
            });
        }

        public void PlaySound(UISoundType type)
        {
            UISoundController.Instance.Play(type);
        }
    }
}
