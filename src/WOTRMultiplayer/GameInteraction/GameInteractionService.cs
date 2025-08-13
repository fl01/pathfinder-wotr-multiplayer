using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Cheats;
using Kingmaker.Controllers.Clicks;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.Controllers.Rest;
using Kingmaker.Controllers.Rest.State;
using Kingmaker.Craft;
using Kingmaker.Designers;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Inspect;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.Localization;
using Kingmaker.Pathfinding;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Settings;
using Kingmaker.TurnBasedMode;
using Kingmaker.TurnBasedMode.Controllers;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UI.Models.Log.Events;
using Kingmaker.UI.MVVM._PCView.Dialog.BookEvent;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Kingmaker.UI.MVVM._PCView.Dialog.Interchapter;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._VM.Dialog.Dialog;
using Kingmaker.UI.MVVM._VM.Rest;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;

namespace WOTRMultiplayer.GameInteraction
{
    public class GameInteractionService : IGameInteractionService
    {
        private readonly ILogger<GameInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IResourceProvider _resourceProvider;
        private readonly IEquipmentDefinitions _equipmentDefinitions;
        private readonly AsyncLocal<RemoteExecutionContext> _networkExecutionContext = new();

        public RemoteExecutionContext RemoteContext => _networkExecutionContext.Value;

        public bool IsPaused => Game.Instance.IsPaused;

        public GameModeType CurrentGameMode => Game.Instance.CurrentMode;

        public string CampingPotionBlueprintRecipeId => Game.Instance.Player.Camping.SelectedPotion?.Item.AssetGuid.ToString();

        public string CampingCookingBlueprintRecipeId => Game.Instance.Player.Camping.CookingRecipe?.AssetGuid.ToString();

        public string CampingScrollBlueprintRecipeId => Game.Instance.Player.Camping.SelectedScroll?.Item.AssetGuid.ToString();

        public bool CampingAutotuneIterationsStatus => Game.Instance.Player.Camping.AutotuneRestIterations;

        public int CampingIterationsCount => Game.Instance.Player.Camping.RestIterationsCount;

        private InGamePCView InGamePCView => (Game.Instance.RootUiContext.m_UIView as InGamePCView);
        private RestPCView RestView => InGamePCView?.m_StaticPartPCView?.m_RestContextPCView?.m_RestPCView;

        public bool IsRandomEncounter => (Game.Instance.RestController.Status?.NightRandomEncounter ?? false) || (Game.Instance.RestController.Status?.WasNightRandomEncounter ?? false);

        public GameInteractionService(
            ILogger<GameInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IEquipmentDefinitions equipmentDefinitions,
            IResourceProvider resourceProvider)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _resourceProvider = resourceProvider;
            _equipmentDefinitions = equipmentDefinitions;
        }


        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var units = networkOvertip.Units.Select(GetUnitEntity).ToList();
                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(units);
                context.Overtip = new OvertipInteractionContext { MapObjectId = networkOvertip.MapObject.Id };
                if (networkOvertip.RequiresEveryoneToMoveMove)
                {
                    context.UnitsMovementContext = new UnitsMovementContext { ShouldMoveEveryone = true };
                }

                var mapObject = GetMapObject(networkOvertip.MapObject.Id);
                if (mapObject == null)
                {
                    _logger.LogError("Unable to perform overtip interaction with missing map object. MapObjectId={mapObjectId}", networkOvertip.MapObject.Id);
                    return;
                }

                if (mapObject.Interactions.Count == 0)
                {
                    var view = FindOvertipForObject(mapObject);
                    if (view is AreaTransitionOvertipView areaTransitionOvertip)
                    {
                        _logger.LogInformation("Interacting with {overtipType}. MapObjectId={mapObjectId}", nameof(AreaTransitionOvertipView), mapObject.UniqueId);
                        areaTransitionOvertip.OnClick();
                        return;
                    }

                    _logger.LogWarning("Unable to find overtip for object with 0 interactions. MapObjectId={mapObjectId}", mapObject.UniqueId);
                    return;
                }

                _logger.LogInformation("Interacting with object via OvertipVM", mapObject.UniqueId);
                // overtips are created by game on demand, but we need to make sure overtip exists
                var overtipVM = new EntityOvertipVM(mapObject, OvertipsView.Instance.GetViewModel() as OvertipsVM);
                // TODO: maybe get exact interactionpart
                overtipVM.Interact(mapObject.Interactions.FirstOrDefault());
                overtipVM.Dispose();
            });
        }



        private OvertipViewBase FindOvertipForObject(MapObjectEntityData mapObject)
        {
            foreach (var kv in OvertipsView.Instance.m_Views)
            {
                var view = kv.Value.FirstOrDefault(v => string.Equals(v.m_ObjectView?.Data.UniqueId, mapObject.UniqueId, StringComparison.OrdinalIgnoreCase));
                if (view != null)
                {
                    return view;
                }
            }

            return null;
        }

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

                _logger.LogInformation("Leaving area. AreaExitId={areaExitId}", areaExitId);
                Game.Instance.LoadArea(areaTransition.AreaEnterPoint, areaTransition.Settings.AutoSaveMode, null);
            });

        }

        public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                ImmediatlyMarkSuggestedDialogAnswers(suggestions);
            });
        }

        public void ResetSuggestedDialogAnswers()
        {
            ImmediatlyMarkSuggestedDialogAnswers([]);
        }
        private void ImmediatlyMarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
        {
            _logger.LogInformation("Marking dialog answer suggestions. Count={count}", suggestions.Count);
            if (Game.Instance.DialogController?.Dialog == null)
            {
                _logger.LogWarning("DialogController.Dialog is null");
                return;
            }

            var dialogContext = (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_DialogContextPCView;
            if (dialogContext == null)
            {
                _logger.LogWarning("DialogContextView is null");
                return;
            }

            switch (Game.Instance.DialogController.Dialog.Type)
            {
                case DialogType.Interchapter:
                    MarkInterchapterAnswer(dialogContext.m_InterchapterPCView, suggestions);
                    break;
                case DialogType.Common:
                    MarkDialogAnswer(dialogContext.m_DialogPCView, suggestions);
                    break;
                case DialogType.Book:
                    MarkBookAnswer(dialogContext.m_BookEventPCView, suggestions);
                    break;
                default:
                    _logger.LogWarning("Marking suggested answers has not been implemented for this dialog type. DialogType={dialogType}", Game.Instance.DialogController.Dialog.Type);
                    break;
            }

            if (suggestions.Count > 0)
            {
                PlaySound(UISoundType.GlobalMapRandomEncounter);
            }
        }
        private void MarkInterchapterAnswer(InterchapterPCView interchapterView, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            if (interchapterView == null)
            {
                return;
            }

            var answers = interchapterView.gameObject.transform.Find("ContentWrapper/Window/Content/Answers");
            MarkAnswers(answers, suggestions);
        }

        private void MarkBookAnswer(BookEventPCView bookView, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            if (bookView == null)
            {
                return;
            }

            var answers = bookView.gameObject.transform.Find("ContentWrapper/Window/Content/Answers");
            MarkAnswers(answers, suggestions);
        }

        private void MarkDialogAnswer(DialogPCView dialogView, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            if (dialogView == null)
            {
                return;
            }

            var answers = dialogView.gameObject.transform.Find("Body/View/Scroll View/Viewport/Content/AnswersPanel");
            MarkAnswers(answers, suggestions);
        }

        private void MarkAnswers(Transform answersContainer, List<NetworkDialogAnswerSuggestion> suggestions)
        {
            const string SuggestionIconName = "SuggestionIcon";

            for (int answerIndex = 0; answerIndex < answersContainer.childCount; answerIndex++)
            {
                var answer = answersContainer.GetChild(answerIndex);
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

                    var rect = suggestionIconObject.GetComponent<RectTransform>();
                    var preferedSize = Math.Min(rect.sizeDelta.x, rect.sizeDelta.y);
                    rect.sizeDelta = new UnityEngine.Vector2(preferedSize, preferedSize);

                    var newPosition = new UnityEngine.Vector3(suggestionIconObject.transform.position.x + 4 - (5 * i), suggestionIconObject.transform.position.y, suggestionIconObject.transform.position.z);
                    suggestionIconObject.transform.SetPositionAndRotation(newPosition, suggestionIconObject.transform.rotation);

                    var image = suggestionIconObject.GetComponent<UnityEngine.UI.Image>();
                    image.color = UnityEngine.Color.white;
                    image.sprite = portrait;
                }
            }
        }

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
        {
            var character = Game.Instance.Player.PartyAndPets.FirstOrDefault(f => string.Equals(f.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                _logger.LogError("Can't move missing character. UnitId={unitId}", unitId);
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                var unityDestination = new UnityEngine.Vector3(destination.X, destination.Y, destination.Z);
                var command = new UnitMoveTo(unityDestination, 0.3f)
                {
                    MovementDelay = delay,
                    Orientation = orientation,
                    CreatedByPlayer = true
                };
                character.Commands.Run(command);
            });
        }

        public void Pause(bool isPaused)
        {
            _logger.LogInformation("Pause game. Value={isPaused}", isPaused);
            if (isPaused)
            {
                Game.Instance.IsPaused = true;
                return;
            }

            Game.Instance.m_WillBePaused = false;
            Game.Instance.PauseOnLoadPendingTicks = 0;
            Game.Instance.IsPaused = false;
        }

        public void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    ResetSuggestedDialogAnswers();

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
            // this is kinda sketchy, but we need to really know if dialog is already in progress
            // starting dialog is really important as it's required to send `NotifyDialogStarted` to clients
            // unfortunately blueprints can be loaded in mainthread only which means we can't get result right away
            // so it's kinda a workaround so caller (MultiplayerHost) could wait to see if `NotifyDialogStarted` needs to be manually triggered
            var hasStartedDialogTask = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Enqueue(() =>
            {
                _logger.LogInformation("Start dialog. DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                    dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
                var dialogBlueprint = Utilities.GetBlueprint<BlueprintDialog>(dialogName);
                var target = GetUnitEntity(targetUnitId);
                var initiator = GetUnitEntity(initiatorUnitId);
                var mapObject = GetMapObject(mapObjectId);
                var speaker = speakerKey == null ? null : new LocalizedString { Key = speakerKey };
                if (dialogBlueprint == null)
                {
                    _logger.LogError("Unable to find dialog. Name={dialogName}", dialogName);
                    return;
                }

                StartDialog(hasStartedDialogTask, dialogBlueprint, initiator, target, mapObject?.View, speaker);
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
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(error, MessageModalBase.ModalType.Message, null));
            });
        }

        public void ShowWarningNotification(string text)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(text, true), true);
            });
        }

        public void AddCombatText(string text)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                Game.Instance.GameLogController.AddReadyEvent(new GameLogEventWarningNotification(text));
            });
        }

        public bool IsUnitAI(string unitId)
        {
            var unit = Game.Instance.Player.PartyAndPets.FirstOrDefault(p => string.Equals(p.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
            return unit == null;
        }

        public List<NetworkUnit> GetUnitsInCombat()
        {
            var unitsToSync = Game.Instance.State.Units.InCombat().ToList();

            UnitEntityData anevia;
            switch (Game.Instance.CurrentlyLoadedArea.name)
            {
                case "Prologue_Caves_1" when (anevia = Game.Instance.State.Units.FirstOrDefault(u => u.CharacterName == "Anevia")) != null:
                    // Anevia, constantly joins midfight
                    unitsToSync.Add(anevia);
                    break;
                default:
                    break;
            }

            var units = unitsToSync
                .Select(c => new NetworkUnit
                {
                    Id = c.UniqueId,
                    Position = new NetworkVector3(c.Position.x, c.Position.y, c.Position.z),
                    Orientation = c.Orientation
                })
                .ToList();

            return units;
        }

        public Task UpdateUnitsAsync(List<NetworkUnit> networkUnits)
        {
            var taskCompletion = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Enqueue(() =>
            {
                foreach (var networkUnit in networkUnits)
                {
                    try
                    {
                        var unit = Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.UniqueId, networkUnit.Id, StringComparison.OrdinalIgnoreCase));
                        if (unit == null)
                        {
                            _logger.LogError("Unable to find specified unit. UnitId={unitId}", networkUnit.Id);
                            continue;
                        }

                        if (!unit.IsInCombat)
                        {
                            _logger.LogWarning("Updating unit outside of the combat. UnitId={unitId}", networkUnit.Id);
                        }

                        if (unit.Orientation != networkUnit.Orientation)
                        {
                            var previousOrientation = unit.Orientation;
                            _logger.LogInformation("Orientation has been updated. UnitId={unitId}, PreviousOrientation={previousOrientation}, NewOrientation={newOrientation}", unit.UniqueId, previousOrientation.ToString("F4"), unit.Orientation.ToString("F4"));
                            unit.Orientation = networkUnit.Orientation;
                        }

                        if (unit.Position.x != networkUnit.Position.X
                            && unit.Position.y != networkUnit.Position.Y
                            && unit.Position.z != networkUnit.Position.Z)
                        {
                            var newPosition = new UnityEngine.Vector3(networkUnit.Position.X, networkUnit.Position.Y, networkUnit.Position.Z);
                            var oldPosition = unit.Position;
                            unit.Translocate(newPosition, unit.Orientation);
                            _logger.LogInformation("Unit position has been updated. UnitId={unitId}, PreviousPosition={oldPosition}, NewPosition={newPosition}", unit.UniqueId, oldPosition.ToString("F4"), newPosition.ToString("F4"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unable to update unit position. UnitId={unitId}", networkUnit.Id);
                        continue;
                    }
                }

                _logger.LogInformation("Finished updating units. UnitsCount={unitsCount}", networkUnits.Count);

                taskCompletion.SetResult(true);
            });

            return taskCompletion.Task;
        }

        public string QuickLoadGame(string savePath)
        {
            var save = LoadSave(savePath);
            _mainThreadAccessor.Enqueue(() =>
            {
                Game.Instance.LoadGame(save);
            });
            return save.GameId;
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

        public void StartTurnBasedCombatTurn(string unitId)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    _logger.LogInformation("Calling CombatController.StartTurn. UnitId={unitId}", unitId);
                    var currentUnit = GetUnitEntity(unitId);
                    if (currentUnit == null)
                    {
                        _logger.LogError("Unable to find unit to call CombatController.StartTurn. UnitId={unitId}", unitId);
                        return;
                    }

                    Game.Instance.TurnBasedCombatController.StartTurn(currentUnit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start CombatController.StartTurn");
                    throw;
                }
            });
        }

        public void StartTurnBasedCombatTurn(bool isActingInSurpriseRound)
        {
            _logger.LogInformation("Starting turn. IsActingInSurpriseRound={isActingInSurpriseRound}", isActingInSurpriseRound);
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    var currentUnit = Game.Instance.TurnBasedCombatController.CurrentTurn.Rider;
                    if (IsUnitAI(currentUnit.UniqueId))
                    {
                        ForceAIRecalculateAction(currentUnit);
                    }

                    Game.Instance.TurnBasedCombatController.CurrentTurn.Start(isActingInSurpriseRound);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start turn");
                    throw;
                }
            });
        }

        private void ForceAIRecalculateAction(UnitEntityData unit)
        {
            Game.Instance.TurnBasedCombatController.UpdateNavigationGrid();
            AiBrainController.Context.ReleaseUnit();
            unit.CombatState.AIData.NextCommandTime = Time.time;
            foreach (UnitCommand unitCommand in unit.Commands.Raw)
            {
                if (unitCommand != null)
                {
                    unitCommand.AiCanInterruptMark = true;
                }
            }
            unit.Commands.InterruptAiCommands();

            Kingmaker.AI.AiBrainController.TickBrain(unit);
        }

        public void EndTurnBasedCombatTurn()
        {
            // TODO: proper command queue
            while ((Game.Instance.TurnBasedCombatController?.CurrentTurn?.Rider?.Commands?.Queue?.Count ?? 0) > 0)
            {
                Thread.Sleep(50);
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? null;
                _logger.LogInformation("Ending combat turn if it's not ending yet. TurnStatus={turnStatus}", turnStatus);
                if (turnStatus != TurnBased.Controllers.TurnController.TurnStatus.Ending && turnStatus != TurnBased.Controllers.TurnController.TurnStatus.Ended)
                {
                    Game.Instance.TurnBasedCombatController.CurrentTurn?.End();
                }
            });
        }

        public void ClickUnit(NetworkClick click)
        {
            try
            {
                var clickUnitHandler = Game.Instance.DefaultPointerController.m_ClickHandlers.FirstOrDefault(c => c is ClickUnitHandler);
                ExecuteClickHandler(clickUnitHandler, click);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate click handler. HandlerTy={handlerType}", typeof(ClickUnitHandler));
                throw;
            }
        }

        public void ClickGroundInCombat(NetworkClick click)
        {
            try
            {
                var clickGroundHandler = Game.Instance.DefaultPointerController.m_ClickHandlers.FirstOrDefault(c => c is ClickGroundHandler);
                ExecuteClickHandler(clickGroundHandler, click);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate click handler. HandlerTy={handlerType}", typeof(ClickGroundHandler));
                throw;
            }
        }

        public void ClickMapObject(NetworkClick click)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    // each client generates a random map object ID, so the easiest way is to look for the nearest bag(assuming its location is relatively the same)
                    var mapObject = click.IsLootBagMapObject ? GetNeareastLootBagMapObject(click.WorldPosition) : GetMapObject(click.MapObjectId);
                    if (mapObject == null)
                    {
                        _logger.LogWarning("Unable to click missing map object. MapObjectId={uniqueId}", click.MapObjectId);
                        return;
                    }

                    var selectedUnits = click.SelectedUnits.Select(GetUnitEntity).ToList();

                    ClickMapObjectHandler.Interact(mapObject.View.gameObject, selectedUnits, forceOvertipInteractions: false, click.MuteEvents);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to interact with map object. MapObjectId={uniqueId}", click.MapObjectId);
                    throw;
                }
            });
        }

        public void ToggleActivatableAbility(NetworkActivatableAbility toggle)
        {
            try
            {
                var caster = GetUnitEntity(toggle.CasterId);
                var ability = FindActivatableAbility(caster, toggle.Id);
                if (ability == null)
                {
                    _logger.LogError("Unable to find activatable ability. UnitId={unitId}, AbilityId={abilityId}", caster.UniqueId, toggle.Id);
                    return;
                }

                var target = GetUnitEntity(toggle.TargetId);
                _mainThreadAccessor.Enqueue(() =>
                {
                    ability.SetIsOn(toggle.IsActive, target);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate ToggleActivatableAbility.  CasterId={casterId}, TargetId={targetId}, AbilityId={abilityId}", toggle.CasterId, toggle.TargetId, toggle.Id);
                throw;
            }
        }

        public void UseAbility(NetworkAbility abilityUse)
        {
            try
            {
                var caster = GetUnitEntity(abilityUse.CasterId);
                var abilityData = FindAbility(caster, abilityUse);
                if (abilityData == null)
                {
                    _logger.LogError("Unable to find ability. UnitId={unitId}, AbilityId={abilityId}, SpellbookBlueprintId={spellbookBlueprintId}", caster.UniqueId, abilityUse.Id, abilityUse.Id);
                    return;
                }

                var target = GetUnitEntity(abilityUse.TargetId);
                var point = new Vector3(abilityUse.TargetPoint.X, abilityUse.TargetPoint.Y, abilityUse.TargetPoint.Z);
                var targetWrapper = new TargetWrapper(point, null, target);

                if (abilityUse.ActionsState != null)
                {
                    UpdateActionsState(abilityUse.ActionsState);
                }

                Enum.TryParse<UnitCommand.CommandType>(abilityUse.CommandType, true, out var commandType);
                var command = UnitUseAbility.CreateCastCommand(abilityData, targetWrapper, commandType);
                command.CreatedByPlayer = true;
                if (abilityUse.VectorPath != null)
                {
                    var movementPath = abilityUse.VectorPath.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
                    command.ForcedPath = new ForcedPath(movementPath);
                    PathVisualizer.Instance.m_CurrentPath = command.ForcedPath;
                    PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);
                }

                _mainThreadAccessor.Enqueue(() =>
                {
                    _logger.LogInformation("Running ability use command. Caster={casterId}, AbilityId={abilityId}", caster.UniqueId, ((UnitUseAbility)command).Ability?.UniqueId);
                    caster.Commands.Run(command);
                });
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Unable to initiate UseAbility.  CasterId={casterId}, TargetId={targetId}, AbilityId={abilityId}", abilityUse.CasterId, abilityUse.TargetId, abilityUse.Id);
                throw;
            }
        }

        public bool CombatTurnHasBeenFinished()
        {
            var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? TurnBased.Controllers.TurnController.TurnStatus.None;
            return turnStatus == TurnBased.Controllers.TurnController.TurnStatus.None
                || turnStatus == TurnBased.Controllers.TurnController.TurnStatus.Ended
                || turnStatus == TurnBased.Controllers.TurnController.TurnStatus.Ending;
        }

        public NetworkActionsState GetActionsState()
        {
            var actionStates = Game.Instance.TurnBasedCombatController.CurrentTurn?.GetActionsStates(Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit);
            if (actionStates == null)
            {
                return null;
            }

            return new NetworkActionsState
            {
                ApproachPoint = new NetworkVector3(actionStates.ApproachPoint.x, actionStates.ApproachPoint.y, actionStates.ApproachPoint.z),
                ApproachRadius = actionStates.ApproachRadius,
                FiveFootStep = CreateNetworkCombatAction(actionStates.FiveFootStep),
                Free = CreateNetworkCombatAction(actionStates.Free),
                Standard = CreateNetworkCombatAction(actionStates.Standard),
                Swift = CreateNetworkCombatAction(actionStates.Swift),
                Move = CreateNetworkCombatAction(actionStates.Move)
            };
        }

        public void CollectContainerLoot(NetworkLootContainer networkLootContainer)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    var mapObject = GetMapObject(networkLootContainer.Id);
                    var lookupTargets = mapObject != null ? [mapObject]
                        : GetNeareastLootableMapObjects(networkLootContainer.Position);

                    foreach (var container in lookupTargets)
                    {
                        var interaction = (InteractionLootPart)container.Interactions.FirstOrDefault(i => i is InteractionLootPart);

                        List<LootTransferPair> transferList = [.. interaction.Loot.Items
                            .Select(item => new LootTransferPair { ItemEntity = item, NetworkItem = networkLootContainer.Items.FirstOrDefault(ni => IsSameItem(item, ni)) })
                            .Where(x => x.NetworkItem != null)];

                        if (transferList.Count == networkLootContainer.Items.Count)
                        {
                            TransferItems(interaction.Loot, Game.Instance.Player.Inventory, transferList);
                            RefreshLootUI();
                            RefreshInventoryWindow();
                            return;
                        }
                    }

                    _logger.LogError("Unable to find valid nearest lootable map object. ContainerId={containerId}, Position={position}", networkLootContainer.Id, networkLootContainer.Position);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to collect container loot");
                    throw;
                }
            });
        }

        public void DropItem(NetworkDropItem dropItem)
        {
            var entity = GetUnitEntity(dropItem.OwnerEntityId);
            if (entity == null)
            {
                _logger.LogError("Unable to find entity to drop item. EntityId={entityId}", dropItem.OwnerEntityId);
                return;
            }

            var possibleItemsToDrop = entity.Inventory
                .Where(i => i.HoldingSlot == null && IsSameItem(i, dropItem.Item))
                .OrderBy(x => x.Count)
                .ToList();

            if (possibleItemsToDrop.Count == 0)
            {
                _logger.LogWarning("Unable to find item to drop. EntityId={entityId}, ItemId={itemId}", dropItem.OwnerEntityId, dropItem.Item.UniqueId);
                return;
            }

            var totalCount = possibleItemsToDrop.Sum(x => x.Count);
            if (totalCount < dropItem.Item.Count)
            {
                _logger.LogError("Not enough items to drop, possibly desynced somewhere else. EntityId={entityId}, ItemId={itemId}, TotalCount={totalCount} RequiredCount={requiredCount}", dropItem.OwnerEntityId, dropItem.Item.UniqueId, totalCount, dropItem.Item.Count);
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                var countToDrop = dropItem.Item.Count;
                foreach (var item in possibleItemsToDrop)
                {
                    var difference = countToDrop - item.Count;
                    if (difference == 0)
                    {
                        DropItem(entity.Inventory, item, dropItem.OwnerEntityId);
                        break;
                    }
                    else if (difference < 0)
                    {
                        var itemsToDrop = item.Split(countToDrop);
                        DropItem(entity.Inventory, itemsToDrop, dropItem.OwnerEntityId);
                        break;
                    }
                    else
                    {
                        // less than needed
                        countToDrop = difference;
                        DropItem(entity.Inventory, item, dropItem.OwnerEntityId);
                        continue;
                    }
                }

                RefreshInventoryWindow();
            });
        }

        private void DropItem(ItemsCollection inventory, ItemEntity itemEntity, string ownerId)
        {
            var itemId = itemEntity.UniqueId;
            using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateDropItem(itemId, ownerId);
            inventory.DropItem(itemEntity);
            _logger.LogInformation("Item has been dropped. EntityId={entityId}, ItemId={itemId}, Count={count}", ownerId, itemId, itemEntity.Count);
        }

        public NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot slot)
        {
            if (slot == null)
            {
                return null;
            }

            var type = slot.GetType();
            var slotType = _equipmentDefinitions.GetSlotType(type);
            if (slotType == null)
            {
                _logger.LogWarning("Unable to get slot type. Type={slotType}, OwnerId={ownerId}", type, slot.Owner.Unit.UniqueId);
                return null;
            }

            // let's just hope that order is the same everytime on everyclient
            var sameTypeItems = slot.Owner.Unit.Body.EquipmentSlots
                .Where(s => s.GetType() == slot.GetType())
                .ToList();

            var slotIndex = sameTypeItems.IndexOf(slot);

            return new NetworkEquipmentSlotPosition
            {
                Index = slotIndex,
                Type = slotType.Value,
            };
        }

        public void UpdateEquipmentSlot(NetworkEquipmentSlot slot)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var unit = GetUnitEntity(slot.OwnerId);
                if (unit == null)
                {
                    _logger.LogError("Unable to update equipment slot for missing unit. UnitId={unitId}", slot.OwnerId);
                    return;
                }

                var slotType = _equipmentDefinitions.GetSlotType(slot.Position.Type);
                if (slotType == null)
                {
                    _logger.LogError("Unable to update equipment slot with invalid slot type. UnitId={unitId}, SlotType={slotType}", slot.OwnerId, slot.Position.Type);
                    return;
                }

                var slotsOfSameType = unit.Body.EquipmentSlots
                    .Where(s => s.GetType() == slotType)
                    .ToList();

                if (slotsOfSameType.Count < slot.Position.Index)
                {
                    _logger.LogError("Unable to update equipment slot with invalid slot index. UnitId={unitId}, SlotType={slotType}, SlotIndex={slotIndex}", slot.OwnerId, slot.Position.Type, slot.Position.Index);
                    return;
                }

                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(slot.Position);

                var slotToUpdate = slotsOfSameType[slot.Position.Index];
                if (slot.Item == null)
                {
                    slotToUpdate.RemoveItem();
                    RefreshInventoryWindow();
                    _logger.LogInformation("Item has been unequipped. UnitId={unitId}, SlotType={slotType}, SlotIndex={slotIndex}", slot.OwnerId, slot.Position.Type, slot.Position.Index);
                    return;
                }

                var item = unit.Inventory.Items.FirstOrDefault(i => string.Equals(i.UniqueId, slot.Item.UniqueId, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    // Stacking / splitting items generates a new item ID, which causes a mismatch on the clients,
                    // so we can find the 'same' unequipped item and equip it
                    var sameItem = unit.Inventory.Items.Where(i => i.HoldingSlot == null && IsSameItem(i, slot.Item))
                        .OrderBy(x => x.Count)
                        .FirstOrDefault();

                    if (sameItem == null)
                    {
                        _logger.LogError("Unable to update equipment slot with missing item. UnitId={unitId}, SlotType={slotType}, SlotIndex={slotIndex}, ItemId={itemId}", slot.OwnerId, slot.Position.Type, slot.Position.Index, slot.Item.UniqueId);
                        return;
                    }

                    // Split only works if count > 1, so it's safe to split everytime
                    item = sameItem.Split(1);
                }

                slotToUpdate.InsertItem(item);
                RefreshInventoryWindow();
                _logger.LogInformation("Item has been equipped. UnitId={unitId}, SlotType={slotType}, SlotIndex={slotIndex}, ItemId={itemId}", slot.OwnerId, slot.Position.Type, slot.Position.Index, slot.Item.UniqueId);
            });
        }

        public void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            var unit = GetUnitEntity(set.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to set active hand equipment set for missing unit. UnitId={unitID}", set.UnitId);
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                if (unit.Body.CurrentHandEquipmentSetIndex == set.Index)
                {
                    return;
                }

                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(set);
                var previousIndex = unit.Body.CurrentHandEquipmentSetIndex;
                unit.Body.CurrentHandEquipmentSetIndex = set.Index;
                RefreshInventoryWindow();
                _logger.LogInformation("Changed active hand equipment slot. UnitId={unitID}, PreviousIndex={previousIndex}, CurrentIndex={currentIndex}", set.UnitId, previousIndex, unit.Body.CurrentHandEquipmentSetIndex);
            });
        }

        public EntityDataBase GetEntity(string id)
        {
            var entity = EntityService.Instance.GetEntity(id);
            return entity;
        }

        public bool IsSummoned(string unitId)
        {
            var unit = GetUnitEntity(unitId);
            return unit.IsSummoned();
        }

        public void ApplyPerceptionCheck(NetworkPerceptionCheck check)
        {
            var mapObject = GetMapObject(check.MapObject.Id);
            if (mapObject == null)
            {
                _logger.LogError("Unable to apply perception check due to missing map object. MapObjectId={mapObjectId}", check.MapObject.Id);
                return;
            }

            var unit = GetUnitEntity(check.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to apply perception check due to missing unit. MapObjectId={mapObjectId}, UnitId={unitId}", check.MapObject.Id, check.UnitId);
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                _logger.LogInformation("Trigerring perception check. MapObjectId={mapObjectId}", check.MapObject.Id);
                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(check);
                PartyPerceptionController.RollPerception(unit, mapObject);
            });
        }

        public void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
            var targetUnit = GetUnitEntity(check.TargetUnitId);
            if (targetUnit == null)
            {
                _logger.LogError("Unable to apply inspection knowledge check due to missing target unit. TargetUnitId={targetUnitId}", check.TargetUnitId);
                return;
            }

            var initiatorUnit = GetUnitEntity(check.InitiatorUnitId);
            if (initiatorUnit == null)
            {
                _logger.LogError("Unable to apply inspection knowledge check due to missing initiator unit. InitiatorUnitId={targetUnitId}", check.InitiatorUnitId);
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                _logger.LogInformation("Applying inspection knowledge check. InitiatorUnitId={initiatorUnitId}, TargetUnitId={targetUnitId}, StatType={statType}", check.InitiatorUnitId, check.TargetUnitId, check.StatType);
                var blueprintForInspection = targetUnit.Descriptor.BlueprintForInspection;
                InspectUnitsManager.UnitInfo info = Game.Instance.Player.InspectUnitsManager.GetInfo(blueprintForInspection);
                if (info == null)
                {
                    info = new InspectUnitsManager.UnitInfo(blueprintForInspection);
                    Game.Instance.Player.InspectUnitsManager.m_UnitInfos.Add(info);
                }

                if (info.IsAllPartsUnlocked)
                {
                    return;
                }

                var ruleSkillCheck = GameHelper.TriggerSkillCheck(new RuleSkillCheck(initiatorUnit, check.StatType, check.DC)
                {
                    IgnoreDifficultyBonusToDC = true
                }, null, true);

                info.SetCheck(ruleSkillCheck.RollResult);
                EventBus.RaiseEvent<IKnowledgeHandler>(x => x.HandleKnowledgeUpdated(info), true);
            });
        }

        public List<string> GetUnitsCombatOrder()
        {
            var units = Game.Instance.TurnBasedCombatController.m_Units.Select(u => u.Unit.UniqueId).ToList();
            return units;
        }

        public string GetNextUnitTurn()
        {
            var nextUnit = Game.Instance.TurnBasedCombatController.m_NextUnit?.UniqueId;
            return nextUnit;
        }

        public void SetNextUnitCombatTurn(string nextUnitId)
        {
            var nextUnit = GetUnitEntity(nextUnitId);
            Game.Instance.TurnBasedCombatController.m_NextUnit = nextUnit;
        }

        public void UpdateCombatOrder(List<string> unitsCombatOrder)
        {
            _logger.LogInformation("Update units combat order. Order={order}", unitsCombatOrder);

            var existingUnits = Game.Instance.TurnBasedCombatController.m_Units.ToList();
            if (unitsCombatOrder.Count != existingUnits.Count)
            {
                _logger.LogError("Combat units mismatch. LocalCount={localCount}, RemoteCount={remoteCount}, LocalUnits={localUnits}", existingUnits.Count, unitsCombatOrder.Count, existingUnits.Select(x => x.Unit.UniqueId));
                return;
            }

            Game.Instance.TurnBasedCombatController.m_Units.Clear();
            foreach (var remoteUnitId in unitsCombatOrder)
            {
                var localUnit = existingUnits.FirstOrDefault(u => string.Equals(remoteUnitId, u.Unit.UniqueId, StringComparison.OrdinalIgnoreCase));
                if (localUnit == null)
                {
                    _logger.LogError("Unable to find unit to set correct combat order. UnitId={unitId}", remoteUnitId);
                    Game.Instance.TurnBasedCombatController.m_Units.Clear();
                    Game.Instance.TurnBasedCombatController.m_Units = [.. existingUnits];
                    return;
                }

                Game.Instance.TurnBasedCombatController.m_Units.Add(localUnit);
            }
        }

        public NetworkGameSettings GetGameSettings()
        {
            var settings = new NetworkGameSettings
            {
                TurnBased = new NetworkTurnBasedSettngs
                {
                    IsTurnBasedModeEnabled = SettingsRoot.Game.TurnBased.EnableTurnBasedMode.GetValue(),
                    AutoEndTurn = SettingsRoot.Game.TurnBased.AutoEndTurn.GetValue(),
                    AutoStopAfterFirstMoveAction = SettingsRoot.Game.TurnBased.AutoStopAfterFirstMoveAction.GetValue(),
                    TimeScaleInPlayerTurn = SettingsRoot.Game.TurnBased.TimeScaleInPlayerTurn.GetValue(),
                    TimeScaleInNonPlayerTurn = SettingsRoot.Game.TurnBased.TimeScaleInNonPlayerTurn.GetValue(),
                },
                Main = new NetworkGameMainSettings
                {
                    LootInCombat = SettingsRoot.Game.Main.LootInCombat.GetValue(),
                    QuickMovement = SettingsRoot.Game.Main.AcceleratedMove.GetValue(),
                },
                Autopause = new NetworkAutopauseSettings
                {
                    ContinueMovementOnEngagement = SettingsRoot.Game.Autopause.ContinueMovementOnEngagement.GetValue(),
                    PauseOnAllyDown = SettingsRoot.Game.Autopause.PauseOnAllyDown.GetValue(),
                    PauseOnAreaLoaded = SettingsRoot.Game.Autopause.PauseOnAreaLoaded.GetValue(),
                    PauseOnAttackOfOpportunity = SettingsRoot.Game.Autopause.PauseOnAttackOfOpportunity.GetValue(),
                    PauseOnEndedBuffSummon = SettingsRoot.Game.Autopause.PauseOnEndedBuffSummon.GetValue(),
                    PauseOnEndOfPartyMembersRound = SettingsRoot.Game.Autopause.PauseOnEndOfPartyMembersRound.GetValue(),
                    PauseOnEndOfRound = SettingsRoot.Game.Autopause.PauseOnEndOfRound.GetValue(),
                    PauseOnEnemyDown = SettingsRoot.Game.Autopause.PauseOnEnemyDown.GetValue(),
                    PauseOnEnemySpotted = SettingsRoot.Game.Autopause.PauseOnEnemySpotted.GetValue(),
                    PauseOnEngagement = SettingsRoot.Game.Autopause.PauseOnEngagement.GetValue(),
                    PauseOnHiddenObjectDetected = SettingsRoot.Game.Autopause.PauseOnHiddenObjectDetected.GetValue(),
                    PauseOnLostFocus = SettingsRoot.Game.Autopause.PauseOnLostFocus.GetValue(),
                    PauseOnLowHealth = SettingsRoot.Game.Autopause.PauseOnLowHealth.GetValue(),
                    PauseOnMeleeEngagement = SettingsRoot.Game.Autopause.PauseOnMeleeEngagement.GetValue(),
                    PauseOnNewEnemyAppeared = SettingsRoot.Game.Autopause.PauseOnNewEnemyAppeared.GetValue(),
                    PauseOnPartyIsAttacked = SettingsRoot.Game.Autopause.PauseOnPartyIsAttacked.GetValue(),
                    PauseOnPartyMemberFinishedAbility = SettingsRoot.Game.Autopause.PauseOnPartyMemberFinishedAbility.GetValue(),
                    PauseOnPartyMemberRanOutOfConsumable = SettingsRoot.Game.Autopause.PauseOnPartyMemberRanOutOfConsumable.GetValue(),
                    PauseOnSpellcastFinished = SettingsRoot.Game.Autopause.PauseOnSpellcastFinished.GetValue(),
                    PauseOnSpellcastInterrupted = SettingsRoot.Game.Autopause.PauseOnSpellcastInterrupted.GetValue(),
                    PauseOnSpellcastStarted = SettingsRoot.Game.Autopause.PauseOnSpellcastStarted.GetValue(),
                    PauseOnTrapDetected = SettingsRoot.Game.Autopause.PauseOnTrapDetected.GetValue(),
                    PauseOnWeaponIsIneffective = SettingsRoot.Game.Autopause.PauseOnWeaponIsIneffective.GetValue(),
                    PauseWhenAllyUnconscious = SettingsRoot.Game.Autopause.PauseWhenAllyUnconscious.GetValue(),
                    PauseWhenEnemyUnconscious = SettingsRoot.Game.Autopause.PauseWhenEnemyUnconscious.GetValue(),
                    PauseWhenLastSleepingEnemyStays = SettingsRoot.Game.Autopause.PauseWhenLastSleepingEnemyStays.GetValue(),
                }
            };

            return settings;
        }

        public void ApplyGameSettings(NetworkGameSettings gameSettings)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                _logger.LogInformation("Applying settings. Settings={settings}", gameSettings);
                SettingsRoot.Game.TurnBased.EnableTurnBasedMode.SetValueAndConfirm(gameSettings.TurnBased.IsTurnBasedModeEnabled);
                SettingsRoot.Game.TurnBased.AutoEndTurn.SetValueAndConfirm(gameSettings.TurnBased.AutoEndTurn);
                SettingsRoot.Game.TurnBased.AutoStopAfterFirstMoveAction.SetValueAndConfirm(gameSettings.TurnBased.AutoStopAfterFirstMoveAction);
                if (gameSettings.TurnBased.TimeScaleInPlayerTurn.HasValue)
                {
                    SettingsRoot.Game.TurnBased.TimeScaleInPlayerTurn.SetValueAndConfirm(gameSettings.TurnBased.TimeScaleInPlayerTurn.Value);
                }
                if (gameSettings.TurnBased.TimeScaleInNonPlayerTurn.HasValue)
                {
                    SettingsRoot.Game.TurnBased.TimeScaleInNonPlayerTurn.SetValueAndConfirm(gameSettings.TurnBased.TimeScaleInNonPlayerTurn.Value);
                }

                SettingsRoot.Game.Main.LootInCombat.SetValueAndConfirm(gameSettings.Main.LootInCombat);
                SettingsRoot.Game.Main.AcceleratedMove.SetValueAndConfirm(gameSettings.Main.QuickMovement);

                SettingsRoot.Game.Autopause.ContinueMovementOnEngagement.SetValueAndConfirm(gameSettings.Autopause.ContinueMovementOnEngagement);
                SettingsRoot.Game.Autopause.PauseOnAllyDown.SetValueAndConfirm(gameSettings.Autopause.PauseOnAllyDown);
                SettingsRoot.Game.Autopause.PauseOnAreaLoaded.SetValueAndConfirm(gameSettings.Autopause.PauseOnAreaLoaded);
                SettingsRoot.Game.Autopause.PauseOnAttackOfOpportunity.SetValueAndConfirm(gameSettings.Autopause.PauseOnAttackOfOpportunity);
                SettingsRoot.Game.Autopause.PauseOnEndedBuffSummon.SetValueAndConfirm(gameSettings.Autopause.PauseOnEndedBuffSummon);
                SettingsRoot.Game.Autopause.PauseOnEndOfPartyMembersRound.SetValueAndConfirm(gameSettings.Autopause.PauseOnEndOfPartyMembersRound);
                SettingsRoot.Game.Autopause.PauseOnEndOfRound.SetValueAndConfirm(gameSettings.Autopause.PauseOnEndOfRound);
                SettingsRoot.Game.Autopause.PauseOnEnemyDown.SetValueAndConfirm(gameSettings.Autopause.PauseOnEnemyDown);
                SettingsRoot.Game.Autopause.PauseOnEnemySpotted.SetValueAndConfirm(gameSettings.Autopause.PauseOnEnemySpotted);
                SettingsRoot.Game.Autopause.PauseOnEngagement.SetValueAndConfirm(gameSettings.Autopause.PauseOnEngagement);
                SettingsRoot.Game.Autopause.PauseOnHiddenObjectDetected.SetValueAndConfirm(gameSettings.Autopause.PauseOnHiddenObjectDetected);
                SettingsRoot.Game.Autopause.PauseOnLostFocus.SetValueAndConfirm(gameSettings.Autopause.PauseOnLostFocus);
                SettingsRoot.Game.Autopause.PauseOnLowHealth.SetValueAndConfirm(gameSettings.Autopause.PauseOnLowHealth);
                SettingsRoot.Game.Autopause.PauseOnMeleeEngagement.SetValueAndConfirm(gameSettings.Autopause.PauseOnMeleeEngagement);
                SettingsRoot.Game.Autopause.PauseOnNewEnemyAppeared.SetValueAndConfirm(gameSettings.Autopause.PauseOnNewEnemyAppeared);
                SettingsRoot.Game.Autopause.PauseOnPartyIsAttacked.SetValueAndConfirm(gameSettings.Autopause.PauseOnPartyIsAttacked);
                SettingsRoot.Game.Autopause.PauseOnPartyMemberFinishedAbility.SetValueAndConfirm(gameSettings.Autopause.PauseOnPartyMemberFinishedAbility);
                SettingsRoot.Game.Autopause.PauseOnPartyMemberRanOutOfConsumable.SetValueAndConfirm(gameSettings.Autopause.PauseOnPartyMemberRanOutOfConsumable);
                SettingsRoot.Game.Autopause.PauseOnSpellcastFinished.SetValueAndConfirm(gameSettings.Autopause.PauseOnSpellcastFinished);
                SettingsRoot.Game.Autopause.PauseOnSpellcastInterrupted.SetValueAndConfirm(gameSettings.Autopause.PauseOnSpellcastInterrupted);
                SettingsRoot.Game.Autopause.PauseOnSpellcastStarted.SetValueAndConfirm(gameSettings.Autopause.PauseOnSpellcastStarted);
                SettingsRoot.Game.Autopause.PauseOnTrapDetected.SetValueAndConfirm(gameSettings.Autopause.PauseOnTrapDetected);
                SettingsRoot.Game.Autopause.PauseOnWeaponIsIneffective.SetValueAndConfirm(gameSettings.Autopause.PauseOnWeaponIsIneffective);
                SettingsRoot.Game.Autopause.PauseWhenAllyUnconscious.SetValueAndConfirm(gameSettings.Autopause.PauseWhenAllyUnconscious);
                SettingsRoot.Game.Autopause.PauseWhenEnemyUnconscious.SetValueAndConfirm(gameSettings.Autopause.PauseWhenEnemyUnconscious);
                SettingsRoot.Game.Autopause.PauseWhenLastSleepingEnemyStays.SetValueAndConfirm(gameSettings.Autopause.PauseWhenLastSleepingEnemyStays);
            });
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var campPosition = new Vector3(position.X, position.Y, position.Z);
                RestHelper.SpawnCampPlace(campPosition);
            });
        }

        public void SetCampingUseHealingSpells(bool isOn)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var restView = RestView;
                if (restView == null)
                {
                    return;
                }

                restView.m_HealingToggle.isOn = isOn;

                var campingState = Game.Instance.Player.Camping;
                if (campingState == null)
                {
                    return;
                }

                campingState.UseSpells = isOn;
            });
        }

        public void SetCampingState(NetworkCampingState state)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    var restView = RestView;
                    if (restView != null)
                    {
                        restView.m_AutotuneToggle.isOn = state.AutotuneIterationsStatus;
                        (restView.GetViewModel() as RestVM)?.HandleIterationsCountCalculated(state.IterationsCount);
                    }

                    var campingState = Game.Instance.Player.Camping;

                    UpdateCookingRecipe(campingState, state.CookingBlueprintRecipeId);
                    UpdateAlchemistRecipe(campingState, state.PotionBlueprintRecipeId);
                    UpdateScrollScribingRecipe(campingState, state.ScrollBlueprintRecipeId);

                    campingState.RestIterationsCount = state.IterationsCount;
                    campingState.m_AutoTuneRestIterations = state.AutotuneIterationsStatus;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during updating camping state");
                    throw;
                }
            });
        }

        public void SetCampingRoles(List<NetworkCampingRole> roles)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var campingState = Game.Instance.Player.Camping;
                if (campingState == null)
                {
                    return;
                }

                foreach (var role in roles)
                {
                    campingState.CurrentCampingRoles[role.RoleType].PrimaryUnit = GetUnitEntity(role.PrimaryUnitId);
                    campingState.CurrentCampingRoles[role.RoleType].SecondaryUnit = GetUnitEntity(role.SecondaryUnitId);
                }
            });
        }

        public void SetStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                if (RestView == null)
                {
                    return;
                }

                _logger.LogInformation("Changing rest button state. IsInteractable={isInteractable}, ReadyPlayers={readyPlayer}, TotalPlayers={totalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                RestView.m_StartRestButton.Interactable = isInteractable;

                var baseText = RestView.m_StartRestButtonText.text;
                if (baseText.EndsWith(")"))
                {
                    var parts = baseText.Split(' ');
                    baseText = string.Join(" ", parts.Take(parts.Length - 1));
                }
                baseText += $" ({readyPlayersCount}/{totalPlayersCount})";
                RestView.m_StartRestButtonText.SetText(baseText);
            });
        }

        public void StartRest()
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                RestView?.StartRest();
            });
        }

        public void SetRandomEncounterContext(NetworkRandomEncounterContext context)
        {
            _networkExecutionContext.Value = RemoteExecutionContext.Create(context);
        }

        public void SetGroundMoveEveryone()
        {
            _networkExecutionContext.Value = new RemoteExecutionContext
            {
                UnitsMovementContext = new UnitsMovementContext { ShouldMoveEveryone = true }
            };
        }

        public void UpdateIsInCombatStatus()
        {
            Game.Instance.Player.UpdateIsInCombat();
            _mainThreadAccessor.Enqueue(() =>
            {
                Game.Instance.Player.UpdateIsInCombat();
            });
        }

        public void TryInterruptRestBanter(NetworkRestBanter networkRestBanter)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    var currentBanterPlayer = Game.Instance.RestController?.m_BanterPlayer;
                    if (currentBanterPlayer == null || currentBanterPlayer.m_NextEntryIndex == 0)
                    {
                        return;
                    }

                    var currentEntryIndex = currentBanterPlayer.m_NextEntryIndex - 1;
                    var currentBark = currentBanterPlayer.m_Entries[currentEntryIndex];
                    if (!string.Equals(currentBark.Text.Key, networkRestBanter.Key, StringComparison.OrdinalIgnoreCase) || !string.Equals(currentBark.Speaker.UniqueId, networkRestBanter.SpeakerUnitId, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("RestController is playing different banter. CurrentBanterKey={currentKey}, CurrentSpeakerUnitId={currentSpeakerUnitId}, NetworkBanterKey={networkBanterKey}, NetworkSpeakerUnitId={networkSpeakerUnitId}",
                                currentBark.Text.Key, currentBark.Speaker.UniqueId, networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                        return;
                    }

                    currentBanterPlayer.InterruptBark();
                    _logger.LogInformation("Rest bark has been interrupted. NetworkBanterKey={networkBanterKey}, NetworkSpeakerUnitId={networkSpeakerUnitId}", networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to interrupd bark. NetworkBanterKey={networkBanterKey}, NetworkSpeakerUnitId={networkSpeakerUnitId}", networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                    throw;
                }
            });
        }

        public void StartTurnBasedCombatTurnAsAnotherUnit(string unitId)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var unit = GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to find unit to start turn. UnitId={unitI}", unitId);
                    return;
                }

                var isSurpriseRound = Game.Instance.TurnBasedCombatController.CurrentTurn.m_ActingInSurpriseRound;
                var previousUnit = Game.Instance.TurnBasedCombatController.CurrentTurn.Rider.UniqueId;
                Game.Instance.TurnBasedCombatController.CurrentTurn.Dispose();

                _logger.LogWarning("Starting turn as another unit due to desync. NewUnitId={unitId}, PreviousUnitId={previousUnit}", unitId, previousUnit);
                Game.Instance.TurnBasedCombatController.CurrentTurn = new TurnBased.Controllers.TurnController(unit);
                Game.Instance.TurnBasedCombatController.CurrentTurn.Start(isSurpriseRound);
            });
        }

        private CraftItemInfo GetCampingCraftItemInfo(UnitEntityData crafter, UsableItemType itemType, string itemBlueprintId)
        {
            if (crafter == null)
            {
                return null;
            }

            var craftingRecipes = Game.Instance.BlueprintRoot.CraftRoot.CollectCraftList(crafter, itemType);
            var recipe = craftingRecipes.FirstOrDefault(c => string.Equals(c.Info.Item.AssetGuid.ToString(), itemBlueprintId, StringComparison.OrdinalIgnoreCase));

            return recipe?.Info;
        }

        private void UpdateScrollScribingRecipe(CampingState campingState, string scrollItemBlueprintId)
        {
            if (string.IsNullOrEmpty(scrollItemBlueprintId))
            {
                campingState.SelectedScroll = null;
                return;
            }

            if (campingState.SelectedScroll != null && string.Equals(campingState.SelectedScroll.Item.AssetGuid.ToString(), scrollItemBlueprintId))
            {
                _logger.LogInformation("Same camping scroll scribing recipe is already selected, skipping updates. BlueprintId={id}", scrollItemBlueprintId);
                return;
            }

            var roleType = CampingRoleType.ScrollScribe;
            var crafter = campingState.GetCharacterByRoleType(roleType);
            if (crafter == null)
            {
                return;
            }

            var recipe = GetCampingCraftItemInfo(crafter, UsableItemType.Scroll, scrollItemBlueprintId);
            if (recipe == null)
            {
                _logger.LogError("Unable update camping scroll scribing recipe due to missing blueprint id. BlueprintId={id}", scrollItemBlueprintId);
                return;
            }

            campingState.SelectedScroll = recipe;

            UpdateCraftingUI(roleType);
        }

        private void UpdateAlchemistRecipe(CampingState campingState, string potionBlueprintRecipeId)
        {
            if (string.IsNullOrEmpty(potionBlueprintRecipeId))
            {
                campingState.SelectedPotion = null;
                return;
            }

            if (campingState.SelectedPotion != null && string.Equals(campingState.SelectedPotion.Item.AssetGuid.ToString(), potionBlueprintRecipeId))
            {
                _logger.LogInformation("Same camping alchemist potion recipe is already selected, skipping updates. BlueprintId={id}", potionBlueprintRecipeId);
                return;
            }

            var roleType = CampingRoleType.Alchemist;
            var crafter = campingState.GetCharacterByRoleType(roleType);
            if (crafter == null)
            {
                return;
            }

            var recipe = GetCampingCraftItemInfo(crafter, UsableItemType.Potion, potionBlueprintRecipeId);
            if (recipe == null)
            {
                _logger.LogError("Unable update camping alchemist potion recipe due to missing blueprint id. BlueprintId={id}", potionBlueprintRecipeId);
                return;
            }

            campingState.SelectedPotion = recipe;

            UpdateCraftingUI(roleType);
        }

        private void UpdateCraftingUI(CampingRoleType campingRoleType)
        {
            EventBus.RaiseEvent<IRestRoleUIStageEvents>(x => x.RestStageClosed(campingRoleType, CraftStage.CraftFirstType, true));
        }

        private void UpdateCookingRecipe(CampingState campingState, string cookingBlueprintRecipeId)
        {
            var cookingRecipe = string.IsNullOrEmpty(cookingBlueprintRecipeId) ? null : campingState.KnownRecipes.FirstOrDefault(r => string.Equals(r.AssetGuid.ToString(), cookingBlueprintRecipeId, StringComparison.OrdinalIgnoreCase));
            campingState.CookingRecipe = cookingRecipe;
        }

        private void RefreshInventoryWindow()
        {
            var inventory = Game.Instance.RootUiContext?.InGameVM?.StaticPartVM?.ServiceWindowsVM?.InventoryVM?.Value;
            if (inventory != null)
            {
                inventory.StashVM?.CollectionChanged();
                inventory.DollVM?.RefreshData();
            }
        }

        private MapObjectEntityData GetNeareastLootBagMapObject(NetworkVector3 position)
        {
            var allNearest = GetNeareastLootableMapObjects(position);
            var lootbag = allNearest.FirstOrDefault(o => o is DroppedLoot.EntityData);
            _logger.LogInformation("Using nearest lootbag as a map object. MapObjectId={mapObjectId}, Position={bagIndex}", lootbag?.UniqueId, lootbag?.Position);
            return lootbag;
        }

        private bool IsSameItem(ItemEntity itemEntity, NetworkItem networkLootItem)
        {
            var sameItemType = string.Equals(itemEntity.NameForAcronym, networkLootItem.Name, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(itemEntity.UniqueId, networkLootItem.UniqueId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemEntity.Blueprint.AssetGuid.ToString(), networkLootItem.BlueprintId, StringComparison.OrdinalIgnoreCase))
                && itemEntity.Cost == networkLootItem.Cost
                && itemEntity.IsLootable
                && itemEntity.EnchantmentValue == networkLootItem.EnchantmentValue
                && itemEntity.Enchantments.FirstOrDefault()?.NameForAcronym == networkLootItem.FirstEnchantmentName
                && itemEntity.Enchantments.Count == networkLootItem.EnchantmentsCount;

            return sameItemType;
        }

        private void RefreshLootUI()
        {
            var lootVm = Game.Instance.RootUiContext?.InGameVM?.StaticPartVM?.LootContextVM?.LootVM?.Value;
            if (lootVm != null)
            {
                foreach (var lootObjectVM in lootVm.ContextLoot)
                {
                    lootObjectVM.UpdateCommand.Execute();
                }
            }
        }

        private void TransferItems(ItemsCollection source, ItemsCollection target, List<LootTransferPair> transferList)
        {
            foreach (var transfer in transferList)
            {
                if (!string.Equals(transfer.ItemEntity.UniqueId, transfer.NetworkItem.UniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Transfer item id is mismatched, updating... ItemId={itemId}, NetworkItemId, ItemName={itemName}, NetworkItemName={networkItemName}",
                        transfer.ItemEntity.UniqueId, transfer.NetworkItem.UniqueId, transfer.ItemEntity.Name, transfer.NetworkItem.Name);

                    transfer.ItemEntity.UniqueId = transfer.NetworkItem.UniqueId;
                }

                source.Transfer(transfer.ItemEntity, transfer.NetworkItem.Count, target);
            }
        }

        private List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position)
        {
            var targetPoint = new Vector3(position.X, position.Y, position.Z);
            var orderedContainers = Game.Instance.State.MapObjects.All
                .Where(o => o.Interactions.Any(i => i is InteractionLootPart))
                .OrderBy(o => (o.Position - targetPoint).magnitude)
                .ToList();

            return orderedContainers;
        }

        private MapObjectEntityData GetMapObject(string uniqueId)
        {
            return Game.Instance.State.MapObjects.All.FirstOrDefault(o => string.Equals(o.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        private NetworkCombatAction CreateNetworkCombatAction(CombatAction action)
        {
            if (action == null)
            {
                return null;
            }

            return new NetworkCombatAction
            {
                MovementActivityStatePredicted = action.m_MovementActivityStatePredicted?.ToString(),
                MovementActivityStateCurrent = action.m_MovementActivityStateCurrent?.ToString(),
                AttackActivityStatePredicted = action.m_AttackActivityStatePredicted?.ToString(),
                AttackActivityStateCurrent = action.m_AttackActivityStateCurrent?.ToString(),
                AbilityActivityStatePredicted = action.m_AbilityActivityStatePredicted?.ToString(),
                AbilityActivityStateCurrent = action.m_AbilityActivityStateCurrent?.ToString(),
                LockType = action.LockType,
                HasMovePossibility = action.HasMovePossibility,
                MaxMoveDistance = action.MaxMoveDistance,
                RemainingMoveDistance = action.RemainingMoveDistance,
                PredictedMoveDistance = action.PredictedMoveDistance,
                Type = action.Type.ToString(),
            };
        }

        private ActivatableAbility FindActivatableAbility(UnitEntityData caster, string id)
        {
            var ability = caster.ActivatableAbilities.Enumerable.FirstOrDefault(a => string.Equals(a.UniqueId, id, StringComparison.OrdinalIgnoreCase));
            return ability;
        }

        private AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityName)
        {
            for (int level = 0; level < spellbook.m_KnownSpells.Length; level++)
            {
                var spellLevel = spellbook.m_KnownSpells[level];
                var spellSlot = spellLevel.FirstOrDefault(s => string.Equals(s.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.NameForAcronym, abilityName, StringComparison.OrdinalIgnoreCase));

                if (spellSlot != null)
                {
                    return spellSlot;
                }
            }

            return null;
        }

        private AbilityData GetMemorizedSpell(Spellbook spellbook, string abilityId, string abilityName)
        {
            for (int level = 0; level < spellbook.m_MemorizedSpells.Length; level++)
            {
                var spellLevel = spellbook.m_MemorizedSpells[level];
                var spellSlot = spellLevel.FirstOrDefault(s => string.Equals(s.Spell?.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Spell?.NameForAcronym, abilityName, StringComparison.OrdinalIgnoreCase));

                if (spellSlot != null)
                {
                    return spellSlot.Spell;
                }
            }

            return null;
        }

        private AbilityData FindAbilityInSpellbook(UnitEntityData unit, NetworkAbility networkAbility)
        {
            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkAbility.SpellbookId));
            if (spellbook == null)
            {
                _logger.LogError("Unable to 4find ability due to missing spellbook. UnitId={unitId}, AbilityId={abilityId}, SpellbookId={spellbookId}", unit.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                return null;
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var spellConversionSource = GetKnownSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name) ?? GetMemorizedSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name);
                if (spellConversionSource == null)
                {
                    _logger.LogError("Can't find spell conversion source for converted ability. UnitId={unitId}, AbilityId={abilityId}, SpellbookName={spellbookName}, ConvertedAbilityId={convertedId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                var convertedSpell = GetConvertedAbility(spellConversionSource, networkAbility);
                if (convertedSpell == null)
                {
                    _logger.LogError("Can't find target ability in spell conversion list. UnitId={unitId}, AbilityId={abilityId}, SpellbookName={spellbookName}, ConvertedAbilityId={convertedId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                _logger.LogInformation("Converted spell has been found. UnitId={unitId}, AbilityId={abilityId}, SpellbookName={spellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return convertedSpell;
            }

            var knownSpell = GetKnownSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (knownSpell != null)
            {
                _logger.LogInformation("Spell has been found in known spells. UnitId={unitId}, AbilityId={abilityId}, SpellbookName={spellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return knownSpell;
            }

            var memorizedSpell = GetMemorizedSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (memorizedSpell != null)
            {
                _logger.LogInformation("Spell has been found in memorized spells. UnitId={unitId}, AbilityId={abilityId}, SpellbookName={spellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return memorizedSpell;
            }

            return null;
        }

        private AbilityData GetConvertedAbility(AbilityData conversionSource, NetworkAbility networkAbility)
        {
            var convertedSpell = conversionSource.GetConversions().FirstOrDefault(
                    c => string.Equals(c.UniqueId, networkAbility.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.NameForAcronym, networkAbility.Name, StringComparison.OrdinalIgnoreCase));

            return convertedSpell;
        }

        /// <summary>
        /// I never thought it would be so rough to find casted ability
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="abilityUse"></param>
        /// <returns></returns>
        private AbilityData FindAbility(UnitEntityData unit, NetworkAbility abilityUse)
        {
            if (!string.IsNullOrEmpty(abilityUse.SpellbookId))
            {
                return FindAbilityInSpellbook(unit, abilityUse);
            }

            if (!string.IsNullOrEmpty(abilityUse.ConvertedFromId))
            {
                var conversionAbility = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, abilityUse.ConvertedFromId, StringComparison.OrdinalIgnoreCase));
                if (conversionAbility == null)
                {
                    _logger.LogInformation("Unable to find ability for conversion. UnitId={unitId}, AbilityId={abilityId}", unit.UniqueId, abilityUse.ConvertedFromId);
                    return null;
                }
                var convertedAbility = GetConvertedAbility(conversionAbility.Data, abilityUse);
                if (convertedAbility == null)
                {
                    _logger.LogInformation("Unable to find ability in conversion list. UnitId={unitId}, AbilityId={abilityId}", unit.UniqueId, abilityUse.ConvertedFromId);
                }

                return convertedAbility;
            }

            var byAbilityId = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, abilityUse.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Data.NameForAcronym, abilityUse.Name, StringComparison.OrdinalIgnoreCase));

            if (byAbilityId != null)
            {
                _logger.LogInformation("Ability has been found by abilityId. UnitId={unitId}, AbilityId={abilityId}", unit.UniqueId, abilityUse.Id);
                return byAbilityId.Data;
            }

            return null;
        }

        private ActionsState GetGameActionsState()
        {
            var selectedUnit = Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit;
            if (selectedUnit == null)
            {
                _logger.LogError("Current turn unit is not selected");
            }

            return Game.Instance.TurnBasedCombatController.CurrentTurn.GetActionsStates(selectedUnit);
        }

        private void ExecuteClickHandler(IClickEventHandler clickEventHandler, NetworkClick click)
        {
            var targetUnit = GetUnitEntity(click.TargetUnitId);
            var selectedUnits = click.SelectedUnits.Select(GetUnitEntity)?.ToList();
            var selectedUnit = selectedUnits.FirstOrDefault();
            var worldPosition = new Vector3(click.WorldPosition.X, click.WorldPosition.Y, click.WorldPosition.Z);

            _mainThreadAccessor.Enqueue(() =>
            {
                try
                {
                    _logger.LogInformation("Executing click handler. Type={handlerType}, WorldPosition={worldPosition}, TargetUnitId={targetUnitId}, SelectedUnit={selectedUnitId}, VectorPathCount={pathCount}",
                               clickEventHandler.GetType().Name, click.WorldPosition, targetUnit?.UniqueId, selectedUnit?.UniqueId, click.VectorPath.Count);

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(selectedUnits);
                    Game.Instance.SelectionCharacter.SelectedUnit.Value = selectedUnit;

                    if (click.ActionsState != null)
                    {
                        UpdateActionsState(click.ActionsState);
                    }

                    if (click.VectorPath != null && click.VectorPath.Count > 0)
                    {
                        var movementPath = click.VectorPath.Select(v => new UnityEngine.Vector3(v.X, v.Y, v.Z)).ToList();
                        // Commands are using m_CurrentPath in case of extra movement is needed, e.g. UnitAttack command with far away target
                        PathVisualizer.Instance.m_CurrentPath = new ForcedPath(movementPath);
                        PathVisualizer.Instance.m_CurrentPathTargetPoint = new UnityEngine.Vector3(click.ActionsState.ApproachPoint.X, click.ActionsState.ApproachPoint.Y, click.ActionsState.ApproachPoint.Z);

                        PathVisualizer.Instance.m_CurrentPath.Claim(this);
                        PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);

                        var pathForCurrentUnit = PathVisualizer.Instance.CurrentPathForUnit(Game.Instance.TurnBasedCombatController.CurrentTurn.SelectedUnit.View)?.vectorPath.Count;
                        _logger.LogInformation("Configured unit path. Vectors={vectorsCount}", pathForCurrentUnit);
                    }

                    clickEventHandler.OnClick(targetUnit?.View?.gameObject, worldPosition, click.Button, simulate: false, click.MuteEvents, IsTMBClick: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to execute click handler. HandlerType={handlerType}", clickEventHandler?.GetType().Name);
                    throw;
                }
            });
        }

        private void UpdateActionsState(NetworkActionsState networkActionsState)
        {
            // could use OwlcatJsonConvert.DeserializeObject instead, but transfering/blidnly using big black box is kinda bad due to zero understanding of whats happening inside and what could cause errors in future
            var actionStates = GetGameActionsState();
            if (actionStates == null)
            {
                _logger.LogError("Unable to update missing actions state");
                return;
            }

            actionStates.ApproachPoint = new UnityEngine.Vector3(networkActionsState.ApproachPoint.X, networkActionsState.ApproachPoint.Y, networkActionsState.ApproachPoint.Z);
            actionStates.ApproachRadius = networkActionsState.ApproachRadius;
            UpdateCombatAction(actionStates.FiveFootStep, networkActionsState.FiveFootStep);
            UpdateCombatAction(actionStates.Free, networkActionsState.Free);
            UpdateCombatAction(actionStates.Move, networkActionsState.Move);
            UpdateCombatAction(actionStates.Standard, networkActionsState.Standard);
            UpdateCombatAction(actionStates.Swift, networkActionsState.Swift);
        }

        private void UpdateCombatAction(CombatAction action, NetworkCombatAction networkCombatAction)
        {
            action.m_MovementActivityStatePredicted = ParseEnum<CombatAction.ActivityState>(networkCombatAction.MovementActivityStatePredicted);
            action.m_MovementActivityStateCurrent = ParseEnum<CombatAction.ActivityState>(networkCombatAction.MovementActivityStateCurrent);
            action.m_AttackActivityStatePredicted = ParseEnum<CombatAction.ActivityState>(networkCombatAction.AttackActivityStatePredicted);
            action.m_AttackActivityStateCurrent = ParseEnum<CombatAction.ActivityState>(networkCombatAction.AttackActivityStateCurrent);
            action.m_AbilityActivityStatePredicted = ParseEnum<CombatAction.ActivityState>(networkCombatAction.AbilityActivityStatePredicted);
            action.m_AbilityActivityStateCurrent = ParseEnum<CombatAction.ActivityState>(networkCombatAction.AbilityActivityStateCurrent);
            action.LockType = networkCombatAction.LockType;
            action.HasMovePossibility = networkCombatAction.HasMovePossibility;
            action.MaxMoveDistance = networkCombatAction.MaxMoveDistance;
            action.RemainingMoveDistance = networkCombatAction.RemainingMoveDistance;
            action.PredictedMoveDistance = networkCombatAction.PredictedMoveDistance;
            action.Type = ParseEnum<CombatAction.UsageType>(networkCombatAction.Type) ?? default;
        }

        private T? ParseEnum<T>(string value)
            where T : struct
        {
            if (Enum.TryParse<T>(value, out var parsed))
            {
                return parsed;
            }

            return null;
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
            if (string.IsNullOrEmpty(uniqueId))
            {
                return null;
            }

            return Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
