using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Cheats;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
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
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.GameInteraction
{
    public class GameInteractionService : IGameInteractionService
    {
        private readonly ILogger<GameInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IResourceProvider _resourceProvider;

        public GameInteractionService(
            ILogger<GameInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IResourceProvider resourceProvider)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _resourceProvider = resourceProvider;
        }

        public bool IsPaused => Game.Instance.IsPaused;

        public string GetSaveGamePath()
        {
            var path = Game.Instance.SaveManager.SavePath;
            return path;
        }

        public SaveInfo LoadSave(string path)
        {
            var save = Game.Instance.SaveManager.LoadZipSave(path);
            return save;
        }

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

                    answer.gameObject.CleanupAllChildren(x => x.name.StartsWith(SuggestionIconName));
                    if (suggestedAnswer == null)
                    {
                        continue;
                    }

                    var portrait = _resourceProvider.GetUISprite("UI_Inventory_IconHeart");
                    var maxIcons = Math.Min(3, suggestedAnswer.Players.Count);
                    for (int i = maxIcons; i > 0; i--)
                    {
                        var arrow = answer.Find("Arrow");
                        var suggestionIconObject = UnityEngine.Object.Instantiate(arrow.gameObject, answer);
                        suggestionIconObject.name = SuggestionIconName + i.ToString();
                        suggestionIconObject.SetActive(true);

                        var rect = suggestionIconObject.GetComponent<UnityEngine.RectTransform>();
                        var preferedSize = Math.Min(rect.sizeDelta.x, rect.sizeDelta.y);
                        rect.sizeDelta = new UnityEngine.Vector2(preferedSize, preferedSize);

                        var newPosition = new UnityEngine.Vector3(suggestionIconObject.transform.position.x + 4 - (5 * i), suggestionIconObject.transform.position.y, suggestionIconObject.transform.position.z);
                        suggestionIconObject.transform.SetPositionAndRotation(newPosition, suggestionIconObject.transform.rotation);

                        var image = suggestionIconObject.GetComponent<Image>();
                        image.color = UnityEngine.Color.white;
                        image.sprite = portrait;
                    }

                    _logger.LogInformation("Created answer suggestion icon. AnswerIndex={answerIndex}, AnswerName={answerName}, PlayersCount={playersCount}", answerIndex, suggestedAnswer.AnswerName, suggestedAnswer.Players.Count);
                }

                // single sound for all suggestions
                if (suggestions.Count > 0)
                {
                    PlaySound(UISoundType.GlobalMapRandomEncounter);
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
            _mainThreadAccessor.Enqueue(() =>
            {
                try
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to select dialog answer");
                    throw;
                }
            });
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

        public Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            _logger.LogInformation("Start dialog. DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
            // this is kinda sketchy, but we need to really know if dialog is already in progress
            // starting dialog is really important as it's required to send `NotifyDialogStarted` to clients
            // unfortunately blueprints can be loaded in mainthread only which means we can't get result right away
            // so it's kinda a workaround so caller (MultiplayerHost) could wait to see if `NotifyDialogStarted` needs to be manually triggered
            var hasStartedDialogTask = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Enqueue(() =>
            {
                var dialogBlueprint = Utilities.GetBlueprint<BlueprintDialog>(dialogName);
                var target = GetUnitEntity(targetUnitId);
                var initiator = GetUnitEntity(initiatorUnitId);
                var mapObject = GetMapObjectView(mapObjectId);
                var speaker = speakerKey == null ? null : new LocalizedString { Key = speakerKey };
                if (dialogBlueprint == null)
                {
                    _logger.LogError("Unable to find dialog. Name={dialogName}", dialogName);
                    return;
                }

                StartDialog(hasStartedDialogTask, dialogBlueprint, initiator, target, mapObject, speaker);
            });

            return hasStartedDialogTask.Task;
        }

        public List<NetworkCharacterOwnership> GetPartyPlayers()
        {
            var partyCharacters = Game.Instance.Player.Party
                .Select(x => new NetworkCharacterOwnership
                {
                    Name = x.CharacterName,
                    Portrait = x.Portrait.SmallPortrait.name,
                    UnitId = x.UniqueId
                })
                .ToList();

            return partyCharacters;
        }

        public void ShowModalMessage(string error)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                EventBus.RaiseEvent<IMessageModalUIHandler>(window =>
                {
                    window.HandleOpen(error, MessageModalBase.ModalType.Message, null);
                });
            });
        }

        public bool IsUnitAI(string unitId)
        {
            var unit = GetUnitEntityFromParty(unitId);
            return unit == null;
        }

        private MapObjectView GetMapObjectView(string mapObjectId)
        {
            if (string.IsNullOrEmpty(mapObjectId))
            {
                return null;
            }

            var mapObject = Game.Instance.State.MapObjects.FirstOrDefault(m => string.Equals(m.UniqueId, mapObjectId, StringComparison.OrdinalIgnoreCase));
            return mapObject?.View;
        }

        private void StartDialog(TaskCompletionSource<bool> hasStartedDialogTask, BlueprintDialog dialog, UnitEntityData initiator, UnitEntityData target, MapObjectView mapObjectView, LocalizedString customSpeakerName)
        {
            if (string.Equals(Game.Instance.DialogController.Dialog?.name, dialog.name, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Requested dialog already started (most likely due to scripted zone), nothing to do here. DialogName={dialogName}", dialog.name);
                hasStartedDialogTask.SetResult(false);
                return;
            }

            Game.Instance.DialogController.StartDialog(dialog, initiator, target, mapObjectView, customSpeakerName);
            hasStartedDialogTask.SetResult(true);
        }

        private UnitEntityData GetUnitEntity(string uniqueId)
        {
            return GetUnitEntityFromLoadedArea(uniqueId) ?? GetUnitEntityFromParty(uniqueId);
        }

        private UnitEntityData GetUnitEntityFromParty(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                return null;
            }

            return Game.Instance.Player.PartyAndPets.FirstOrDefault(p => string.Equals(p.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        private UnitEntityData GetUnitEntityFromLoadedArea(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                return null;
            }

            return Game.Instance.LoadedAreaState.AllEntityData.FirstOrDefault(e => string.Equals(e.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase)) as UnitEntityData;
        }

        public List<NetworkUnit> GetUnitsInCombat()
        {
            var inCombat = Game.Instance.State.Units
                .InCombat()
                .Select(c => new NetworkUnit { Id = c.UniqueId, Position = new Vector3(c.Position.x, c.Position.y, c.Position.z) })
                .ToList();

            return inCombat;
        }

        public void UpdateUnitsPosition(List<NetworkUnit> networkUnits)
        {
            foreach (var networkUnit in networkUnits)
            {
                var unit = Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.UniqueId, networkUnit.Id, StringComparison.OrdinalIgnoreCase));
                if (unit == null)
                {
                    _logger.LogError("Unable to find specified unit. UnitId={unitId}", networkUnit.Id);
                    continue;
                }

                if (!unit.IsInCombat)
                {
                    _logger.LogError("Updating position for unit outside of the combat. UnitId={unitId}", networkUnit.Id);
                }

                if (unit.Position.x == networkUnit.Position.X
                    && unit.Position.y == networkUnit.Position.Y
                    && unit.Position.z == networkUnit.Position.Z)
                {
                    _logger.LogInformation("Unit position has no need to be updated. UnitId={unitId}");
                    continue;
                }

                var oldPosition = unit.Position;
                unit.Position = new UnityEngine.Vector3(networkUnit.Position.X, networkUnit.Position.Y, networkUnit.Position.Z);
                _logger.LogInformation("Unit position has been updated. UnitId={unitId}, OldPosition={oldPosition}, NewPosition={newPosition}", unit.UniqueId, oldPosition, unit.Position);
            }
        }

        public void QuickLoadGame(string savePath)
        {
            var save = LoadSave(savePath);
            _mainThreadAccessor.Enqueue(() =>
            {
                Game.Instance.LoadGame(save);
            });
        }

        public void LoadGameFromMainMenu(string savePath)
        {
            var save = LoadSave(savePath);
            _mainThreadAccessor.Enqueue(() =>
            {
                Game.Instance.RootUiContext.MainMenuVM.EnterLoadGame(save);
            });
        }

        public string GetPetOwnerId(string unitId)
        {
            var unit = Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.UniqueId, unitId));

            if (unit == null)
            {
                return null;
            }

            if (unit.IsPet && unit.Master == null)
            {
                _logger.LogError("Pet has no master. UnitId={unitId}", unitId);
                return null;
            }

            return unit.IsPet ? unit.Master.UniqueId : null;
        }

        public void StartTurnBasedCombatTurn(bool isActingInSurpriseRound)
        {
            _logger.LogInformation("Starting turn. IsActingInSurpriseRound={isActingInSurpriseRound}", isActingInSurpriseRound);
            _mainThreadAccessor.Enqueue(() =>
            {
                Game.Instance.TurnBasedCombatController.CurrentTurn.Start(isActingInSurpriseRound);
            });
        }

        public void EndTurnBasedCombatTurn()
        {
            _logger.LogInformation("Ending turn.");
            _mainThreadAccessor.Enqueue(Game.Instance.TurnBasedCombatController.CurrentTurn.End);
        }
    }
}
