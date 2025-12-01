using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Blueprints;
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
using Kingmaker.EntitySystem.Stats;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.Inspect;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.Localization;
using Kingmaker.Pathfinding;
using Kingmaker.PubSubSystem;
using Kingmaker.RandomEncounters;
using Kingmaker.RandomEncounters.Settings;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Settings;
using Kingmaker.TurnBasedMode;
using Kingmaker.TurnBasedMode.Controllers;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UI.Common;
using Kingmaker.UI.Models.Log.Events;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.AbilityScores;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Class;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.FeatureSelector;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Skills;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Spells;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.Common.MessageModal;
using Kingmaker.UI.MVVM._PCView.Dialog.BookEvent;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Kingmaker.UI.MVVM._PCView.Dialog.Interchapter;
using Kingmaker.UI.MVVM._PCView.GlobalMap;
using Kingmaker.UI.MVVM._PCView.GroupChanger;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._VM.CharGen.Phases;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.FeatureSelector;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Spells;
using Kingmaker.UI.MVVM._VM.Lockpick;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UI.MVVM._VM.Vendor;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UniRx;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Units;
using WOTRMultiplayer.MP.Entities.Vendor;
using WOTRMultiplayer.Settings;

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

        public GameModeType CurrentGameMode => Game.Instance.CurrentMode;

        private InGamePCView InGamePCView => (Game.Instance.RootUiContext.m_UIView as InGamePCView);
        private GlobalMapPCView GlobalMapPCView => (Game.Instance.RootUiContext.m_UIView as GlobalMapPCView);
        private PartyPCView PartyPCView => InGamePCView?.m_StaticPartPCView?.m_PartyPCView ?? GlobalMapPCView?.m_PartyPCView;
        private SkipTimePCView SkipTimeView => InGamePCView?.m_StaticPartPCView?.m_SkipTimePCView ?? GlobalMapPCView?.m_SkipTimePCView;
        private RestPCView RestView => InGamePCView?.m_StaticPartPCView?.m_RestContextPCView?.m_RestPCView ?? GlobalMapPCView?.m_RestPCView;
        private GroupChangerPCView GroupChangerView => (InGamePCView?.m_StaticPartPCView?.m_GroupChangerContextPCView ?? GlobalMapPCView?.m_GroupChangerContextPCView)?.m_GroupChangerPCView;
        private VendorVM VendorViewVM => InGamePCView?.m_StaticPartPCView?.m_VendorPCView?.ViewModel;
        private SpellbookMemorizingPanelVM SpellbookMemorizingVM => (InGamePCView?.m_StaticPartPCView?.m_ServiceWindowsPCView ?? GlobalMapPCView.m_ServiceWindowsPCView)?.m_SpellbookPCView?.m_MemorizingPanelView?.ViewModel;
        private CharGenPCView CharGenView => (InGamePCView?.m_StaticPartPCView?.m_CharGenContextPCView ?? GlobalMapPCView?.m_CharGenContextPCView)?.m_CharGenPCView;

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

        public NetworkCampingState GetCampigState()
        {
            var camping = Game.Instance.Player.Camping;
            var state = new NetworkCampingState
            {
                PotionBlueprintRecipeId = camping.SelectedPotion?.Item.AssetGuid.ToString(),
                CookingBlueprintRecipeId = camping.CookingRecipe?.AssetGuid.ToString(),
                ScrollBlueprintRecipeId = camping.SelectedScroll?.Item.AssetGuid.ToString(),
                AutotuneIterationsStatus = camping.AutotuneRestIterations,
                IterationsCount = camping.RestIterationsCount
            };

            return state;
        }

        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
            _mainThreadAccessor.Post(() =>
            {
                var units = networkOvertip.Units.Select(GetUnitEntity).ToList();
                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(units);
                context.Overtip = new OvertipInteractionContext { MapObjectId = networkOvertip.MapObject.Id };
                if (networkOvertip.RequiresEveryoneToMoveMove)
                {
                    context.UnitsMovement = new UnitsMovementContext { ShouldMoveEveryone = true };
                }

                var mapObject = GetMapObject(networkOvertip.MapObject.Id);
                if (mapObject == null)
                {
                    _logger.LogError("Unable to perform overtip interaction with missing map object. MapObjectId={MapObjectId}", networkOvertip.MapObject.Id);
                    return;
                }

                if (mapObject.Interactions.Count == 0)
                {
                    var view = FindOvertipForObject(mapObject);
                    // game doesn't render overtips if it's not visible on your screen
                    var areaTransitionComponent = mapObject.View.GetComponent<AreaTransition>();
                    if (view == null && areaTransitionComponent != null)
                    {
                        var transitionOvertipVM = new EntityOvertipVM(mapObject, OvertipsView.Instance.ViewModel);
                        OvertipsView.Instance.AddMapObject(transitionOvertipVM);
                        view = FindOvertipForObject(mapObject);
                    }

                    if (view is AreaTransitionOvertipView areaTransitionOvertip)
                    {
                        _logger.LogInformation("Interacting with {overtipType}. MapObjectId={MapObjectId}", nameof(AreaTransitionOvertipView), mapObject.UniqueId);
                        areaTransitionOvertip.OnClick();
                        return;
                    }

                    _logger.LogWarning("Unable to interact with target object. MapObjectId={MapObjectId}", mapObject.UniqueId);
                    return;
                }

                _logger.LogInformation("Interacting with object via OvertipVM", mapObject.UniqueId);
                // overtips are created by game on demand, but we need to make sure overtip exists
                var overtipVM = new EntityOvertipVM(mapObject, OvertipsView.Instance.ViewModel);
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
            _mainThreadAccessor.Post(() =>
            {
                var allTransitions = Game.Instance.State.MapObjects.All.Select(o => o.View.GetComponent<AreaTransition>()).Where(t => t != null).ToList();
                var transition = allTransitions.FirstOrDefault(x => string.Equals(x.GetComponent<MapObjectView>().UniqueId, areaExitId, System.StringComparison.OrdinalIgnoreCase));
                var areaTransition = transition?.GetComponent<MapObjectView>()?.Data.Get<AreaTransitionPart>();
                if (areaTransition == null)
                {
                    _logger.LogError("Unable to find requested area transition. TransitionsCount={TransitionsCount}, AreaExitId={AreaExitId}", allTransitions.Count, areaExitId);
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

                _logger.LogInformation("Leaving area. AreaExitId={AreaExitId}", areaExitId);
                Game.Instance.LoadArea(areaTransition.AreaEnterPoint, areaTransition.Settings.AutoSaveMode, null);
            });

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
        private void ImmediatlyMarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
        {
            _logger.LogInformation("Marking dialog answer suggestions. Count={Count}", suggestions.Count);
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
                    _logger.LogWarning("Marking suggested answers has not been implemented for this dialog type. DialogType={DialogType}", Game.Instance.DialogController.Dialog.Type);
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
                var answerName = answerView.ViewModel.Answer.Value.name;
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

        public void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove)
        {
            var character = Game.Instance.Player.PartyAndPets.FirstOrDefault(f => string.Equals(f.UniqueId, networkCharacterMove.UnitId, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                _logger.LogError("Can't move missing character. UnitId={UnitId}", networkCharacterMove.UnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                var unityDestination = new UnityEngine.Vector3(networkCharacterMove.Destination.X, networkCharacterMove.Destination.Y, networkCharacterMove.Destination.Z);
                var command = new UnitMoveTo(unityDestination, 0.3f)
                {
                    MovementDelay = networkCharacterMove.Delay,
                    Orientation = networkCharacterMove.Orientation,
                    CreatedByPlayer = true
                };
                character.Commands.Run(command);
            });
        }

        public void Pause(bool isPaused)
        {
            _logger.LogInformation("Pause game. IsPaused={IsPaused}", isPaused);
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
            _mainThreadAccessor.Post(() =>
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

                    _logger.LogInformation("Dialog continue button updated. IsInteractable={IsInteractable}, HotkeysEnabled={HotkeysEnabled}", isEnabled, hotkeysEnabled);
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
            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Start dialog. DialogName={DialogName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, MapObjectId={MapObjectId}, SpeakerKey={SpeakerKey}",
                    dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
                var dialogBlueprint = Utilities.GetBlueprint<BlueprintDialog>(dialogName);
                var target = GetUnitEntity(targetUnitId);
                var initiator = GetUnitEntity(initiatorUnitId);
                var mapObject = GetMapObject(mapObjectId);
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

        public void ShowModalMessage(string messageKey, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                var message = GetLocalizedText(messageKey, args);
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message, MessageModalBase.ModalType.Message, null));
            });
        }

        public void ShowWarningNotification(string messageKey, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                var message = GetLocalizedText(messageKey, args);
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, true), true);
            });
        }

        public void AddCombatText(string messageKey, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                var message = GetLocalizedText(messageKey, args);
                Game.Instance.GameLogController.AddReadyEvent(new GameLogEventWarningNotification(message));
            });
        }

        public bool IsUnitAI(string unitId)
        {
            var unit = Game.Instance.Player.PartyAndPets.FirstOrDefault(p => string.Equals(p.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
            return unit == null;
        }

        public NetworkCombatState GetCombatState()
        {
            var state = new NetworkCombatState
            {
                RoundNumber = Game.Instance.TurnBasedCombatController.RoundNumber,
                HasSurpriseRound = Game.Instance.TurnBasedCombatController.m_HasSurpriseRound,
                Units = GetUnitsInCombat()
            };

            return state;
        }

        public Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate)
        {
            var taskCompletion = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Updating combat state");
                    var requiresExtraLog = false;
                    if (requiresFullUpdate && Game.Instance.TurnBasedCombatController.m_HasSurpriseRound != networkCombatState.HasSurpriseRound)
                    {
                        _logger.LogWarning("Surprise round difference synced. PreviousSurpriseState={PreviousSurpriseState}, NewSurpriseState={NewSurpriseState}", Game.Instance.TurnBasedCombatController.m_HasSurpriseRound, networkCombatState.HasSurpriseRound);
                        Game.Instance.TurnBasedCombatController.m_HasSurpriseRound = networkCombatState.HasSurpriseRound;
                        requiresExtraLog = true;
                    }

                    if (requiresFullUpdate && Game.Instance.TurnBasedCombatController.RoundNumber != networkCombatState.RoundNumber)
                    {
                        _logger.LogWarning("RoundNumber difference synced. PreviousRoundNumber={PreviousRoundNumber}, NewRoundNumber={NewRoundNumber}", Game.Instance.TurnBasedCombatController.RoundNumber, networkCombatState.RoundNumber);
                        Game.Instance.TurnBasedCombatController.RoundNumber = networkCombatState.RoundNumber;
                        requiresExtraLog = true;
                    }

                    UpdateCombatState(networkCombatState, requiresFullUpdate);

                    if (requiresExtraLog)
                    {
                        Game.Instance.TurnBasedCombatController.LogRound();
                    }

                    taskCompletion.SetResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating combat state");
                    throw;
                }
            });

            return taskCompletion.Task;
        }

        public string QuickLoadGame(string savePath)
        {
            var save = LoadSave(savePath);
            _mainThreadAccessor.Post(() =>
            {
                Game.Instance.LoadGame(save);
            });
            return save.GameId;
        }

        public string LoadGameFromMainMenu(string savePath)
        {
            var save = LoadSave(savePath);
            _mainThreadAccessor.Post(() =>
            {
                Game.Instance.RootUiContext.MainMenuVM.EnterLoadGame(save);
            });

            return save.GameId;
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
                _logger.LogError("Pet has no master. UnitId={UnitId}", unitId);
                return null;
            }

            return unit.IsPet ? unit.Master.UniqueId : null;
        }

        public void StartTurnBasedCombatTurn(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Calling CombatController.StartTurn. UnitId={UnitId}", unitId);
                    var currentUnit = GetUnitEntity(unitId);
                    if (currentUnit == null)
                    {
                        _logger.LogError("Unable to find unit to call CombatController.StartTurn. UnitId={UnitId}", unitId);
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

        public void EndTurnBasedCombatTurn()
        {
            // TODO: proper command queue
            while ((Game.Instance.TurnBasedCombatController?.CurrentTurn?.Rider?.Commands?.Queue?.Count ?? 0) > 0)
            {
                Thread.Sleep(50);
            }

            _mainThreadAccessor.Post(() =>
            {
                var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? null;
                _logger.LogInformation("Ending combat turn if it's not ending yet. TurnStatus={TurnStatus}", turnStatus);
                if (turnStatus != TurnBased.Controllers.TurnController.TurnStatus.Ending && turnStatus != TurnBased.Controllers.TurnController.TurnStatus.Ended)
                {
                    Game.Instance.TurnBasedCombatController.CurrentTurn?.End();
                }
            });
        }

        public void ClickUnit(NetworkClick networkClick)
        {
            try
            {
                var clickUnitHandler = Game.Instance.DefaultPointerController.m_ClickHandlers.FirstOrDefault(c => c is ClickUnitHandler);
                ExecuteClickHandler(clickUnitHandler, networkClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate click handler. HandlerType={HandlerType}", typeof(ClickUnitHandler));
                throw;
            }
        }

        public void ClickGroundInCombat(NetworkClick networkClick)
        {
            try
            {
                var clickGroundHandler = Game.Instance.DefaultPointerController.m_ClickHandlers.FirstOrDefault(c => c is ClickGroundHandler);
                ExecuteClickHandler(clickGroundHandler, networkClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate click handler. HandlerType={HandlerType}", typeof(ClickGroundHandler));
                throw;
            }
        }

        public void ClickMapObject(NetworkClick networkClick)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    // each client generates a random map object ID, so the easiest way is to look for the nearest bag(assuming its location is relatively the same)
                    var mapObject = networkClick.IsLootBagMapObject ? GetNeareastLootBagMapObject(networkClick.WorldPosition) : GetMapObject(networkClick.MapObjectId);
                    if (mapObject == null)
                    {
                        _logger.LogWarning("Unable to click missing map object. MapObjectId={MapObjectId}", networkClick.MapObjectId);
                        return;
                    }

                    var selectedUnits = networkClick.SelectedUnits.Select(GetUnitEntity).ToList();

                    ClickMapObjectHandler.Interact(mapObject.View.gameObject, selectedUnits, forceOvertipInteractions: false, networkClick.MuteEvents);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to interact with map object. MapObjectId={MapObjectId}", networkClick.MapObjectId);
                    throw;
                }
            });
        }

        public void ToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility)
        {
            try
            {
                var caster = GetUnitEntity(networkActivatableAbility.CasterId);
                var ability = FindActivatableAbility(caster, networkActivatableAbility);
                if (ability == null)
                {
                    _logger.LogError("Unable to find activatable ability. UnitId={UnitId}, AbilityId={AbilityId}", caster.UniqueId, networkActivatableAbility.Id);
                    return;
                }

                var target = GetUnitEntity(networkActivatableAbility.TargetId);
                _mainThreadAccessor.Post(() =>
                {
                    ability.SetIsOn(networkActivatableAbility.IsActive, target);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate ToggleActivatableAbility.  CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}", networkActivatableAbility.CasterId, networkActivatableAbility.TargetId, networkActivatableAbility.Id);
                throw;
            }
        }

        public void UseAbility(NetworkAbility networkAbility)
        {
            try
            {
                var caster = GetUnitEntity(networkAbility.CasterId);
                var abilityData = FindAbility(caster, networkAbility);
                if (abilityData == null)
                {
                    _logger.LogError("Unable to find ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookBlueprintId={SpellbookBlueprintId}", caster.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                    return;
                }

                var target = GetUnitEntity(networkAbility.TargetId);
                var point = new Vector3(networkAbility.TargetPoint.X, networkAbility.TargetPoint.Y, networkAbility.TargetPoint.Z);
                var targetWrapper = new TargetWrapper(point, null, target);

                if (networkAbility.ActionsState != null)
                {
                    //UpdateActionsState(networkAbility.ActionsState);
                }

                System.Enum.TryParse<UnitCommand.CommandType>(networkAbility.CommandType, true, out var commandType);
                var command = UnitUseAbility.CreateCastCommand(abilityData, targetWrapper, commandType);
                command.CreatedByPlayer = true;
                if (networkAbility.VectorPath != null)
                {
                    var movementPath = networkAbility.VectorPath.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
                    command.ForcedPath = new ForcedPath(movementPath);
                    PathVisualizer.Instance.m_CurrentPath = command.ForcedPath;
                    PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);
                }

                _mainThreadAccessor.Post(() =>
                {
                    _logger.LogInformation("Running ability use command. Caster={Caster}, AbilityId={AbilityId}", caster.UniqueId, ((UnitUseAbility)command).Ability?.UniqueId);
                    caster.Commands.Run(command);
                });
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Unable to initiate UseAbility.  CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}", networkAbility.CasterId, networkAbility.TargetId, networkAbility.Id);
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

        public void CollectLootContainer(NetworkLootContainer networkLootContainer)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var lookupTargets = GetLootContainers(networkLootContainer);
                    foreach (var container in lookupTargets)
                    {
                        List<LootTransferPair> transferList = [.. container.Items
                            .Select(item => new LootTransferPair { ItemEntity = item, NetworkItem = networkLootContainer.Items.FirstOrDefault(ni => IsSameItem(item, ni)) })
                            .Where(x => x.NetworkItem != null)];

                        if (transferList.Count == networkLootContainer.Items.Count)
                        {
                            TransferItems(container, Game.Instance.Player.Inventory, transferList);
                            RefreshLootUI();
                            RefreshInventoryWindow();
                            return;
                        }
                    }

                    _logger.LogError("Unable to find valid lootable unit / map object. ContainerId={ContainerId}, Position={Position}, IsUnit={IsUnit}", networkLootContainer.Id, networkLootContainer.Position, networkLootContainer.IsUnit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to collect container loot");
                    throw;
                }
            });
        }

        public void SkinLootContainer(NetworkLootContainer networkLootContainer)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var unit = GetUnitEntity(networkLootContainer.Id);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to find unit to skin. UnitId={UnitId}, Position={Position}", networkLootContainer.Id, networkLootContainer.Position);
                        return;
                    }

                    var itemsToSkin = unit.Inventory.Where(i => i.NeedSkinningForCollect).ToList();
                    foreach (var item in itemsToSkin)
                    {
                        item.UseSkinning();
                    }

                    RefreshLootUI();
                    RefreshInventoryWindow();
                    _logger.LogInformation("Loot container has been skinned. ContainerId={ContainerId}, Position={Position}", networkLootContainer.Id, networkLootContainer.Position);

                    //var mapObject = GetMapObject(networkLootContainer.Id);
                    //var lookupTargets = mapObject != null ? [mapObject]
                    //    : GetNeareastLootableMapObjects(networkLootContainer.Position);

                    //foreach (var container in lookupTargets)
                    //{
                    //    var interaction = (InteractionLootPart)container.Interactions.FirstOrDefault(i => i is InteractionLootPart);
                    //    var itemsToSkin = interaction.Loot.Where((ItemEntity i) => i.NeedSkinningForCollect).ToList();
                    //    foreach (var item in itemsToSkin)
                    //    {
                    //        item.UseSkinning();
                    //    }

                    //    if (itemsToSkin.Count > 0)
                    //    {
                    //        RefreshLootUI();
                    //        RefreshInventoryWindow();
                    //        _logger.LogInformation("Loot container has been skinned. ContainerId={ContainerId}, Position={Position}", networkLootContainer.Id, networkLootContainer.Position);
                    //        return;
                    //    }
                    //}

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to use skinning on loot container");
                    throw;
                }
            });
        }

        public void DropItem(NetworkDropItem networkDropItem)
        {
            var entity = GetUnitEntity(networkDropItem.OwnerEntityId);
            if (entity == null)
            {
                _logger.LogError("Unable to find entity to drop item. EntityId={EntityId}", networkDropItem.OwnerEntityId);
                return;
            }

            var possibleItemsToDrop = entity.Inventory
                .Where(i => i.HoldingSlot == null && IsSameItem(i, networkDropItem.Item))
                .OrderBy(x => x.Count)
                .ToList();

            if (possibleItemsToDrop.Count == 0)
            {
                _logger.LogWarning("Unable to find item to drop. EntityId={EntityId}, ItemId={ItemId}", networkDropItem.OwnerEntityId, networkDropItem.Item.UniqueId);
                return;
            }

            var totalCount = possibleItemsToDrop.Sum(x => x.Count);
            if (totalCount < networkDropItem.Item.Count)
            {
                _logger.LogError("Not enough items to drop, possibly desynced somewhere else. EntityId={EntityId}, ItemId={ItemId}, TotalCount={TotalCount}, RequiredCount={RequiredCount}", networkDropItem.OwnerEntityId, networkDropItem.Item.UniqueId, totalCount, networkDropItem.Item.Count);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                MatchSameNumberOfItems(possibleItemsToDrop, networkDropItem.Item.Count,
                    (item) => DropItem(entity.Inventory, item, networkDropItem.OwnerEntityId));

                RefreshInventoryWindow();
            });
        }

        private void MatchSameNumberOfItems(List<ItemEntity> possibleItemsBag, int countToDrop, Action<ItemEntity> onMatched)
        {
            foreach (var item in possibleItemsBag)
            {
                var difference = countToDrop - item.Count;
                if (difference == 0)
                {
                    onMatched(item);
                    break;
                }
                else if (difference < 0)
                {
                    var itemToDrop = item.Split(countToDrop);
                    onMatched(itemToDrop);
                    break;
                }
                else
                {
                    // less than needed
                    countToDrop = difference;
                    onMatched(item);
                }
            }
        }

        private void DropItem(ItemsCollection inventory, ItemEntity itemEntity, string ownerId)
        {
            var itemId = itemEntity.UniqueId;
            using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateDropItem(itemId, ownerId);
            inventory.DropItem(itemEntity);
            _logger.LogInformation("Item has been dropped. EntityId={EntityId}, ItemId={ItemId}, Count={Count}", ownerId, itemId, itemEntity.Count);
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
                _logger.LogWarning("Unable to get slot type. SlotType={slotType}, OwnerId={OwnerId}", type, slot.Owner.Unit.UniqueId);
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

        public void UpdateEquipmentSlot(NetworkEquipmentSlot networkEquipmentSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = GetUnitEntity(networkEquipmentSlot.OwnerId);
                if (unit == null)
                {
                    _logger.LogError("Unable to update equipment slot for missing unit. UnitId={UnitId}", networkEquipmentSlot.OwnerId);
                    return;
                }

                var slotType = _equipmentDefinitions.GetSlotType(networkEquipmentSlot.Position.Type);
                if (slotType == null)
                {
                    _logger.LogError("Unable to update equipment slot with invalid slot type. UnitId={UnitId}, SlotType={SlotType}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type);
                    return;
                }

                var slotsOfSameType = unit.Body.EquipmentSlots
                    .Where(s => s.GetType() == slotType)
                    .ToList();

                if (slotsOfSameType.Count < networkEquipmentSlot.Position.Index)
                {
                    _logger.LogError("Unable to update equipment slot with invalid slot index. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index);
                    return;
                }

                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(networkEquipmentSlot.Position);

                var slotToUpdate = slotsOfSameType[networkEquipmentSlot.Position.Index];
                if (networkEquipmentSlot.Item == null)
                {
                    slotToUpdate.RemoveItem();
                    RefreshInventoryWindow();
                    _logger.LogInformation("Item has been unequipped. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index);
                    return;
                }

                var item = unit.Inventory.Items.FirstOrDefault(i => string.Equals(i.UniqueId, networkEquipmentSlot.Item.UniqueId, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    // Stacking / splitting items generates a new item ID, which causes a mismatch on the clients,
                    // so we can find the 'same' unequipped item and equip it
                    var sameItem = unit.Inventory.Items.Where(i => i.HoldingSlot == null && IsSameItem(i, networkEquipmentSlot.Item))
                        .OrderBy(x => x.Count)
                        .FirstOrDefault();

                    if (sameItem == null)
                    {
                        _logger.LogError("Unable to update equipment slot with missing item. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index, networkEquipmentSlot.Item.UniqueId);
                        return;
                    }

                    // Split only works if count > 1, so it's safe to split everytime
                    item = sameItem.Split(1);
                }

                slotToUpdate.InsertItem(item);
                RefreshInventoryWindow();
                _logger.LogInformation("Item has been equipped. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index, networkEquipmentSlot.Item.UniqueId);
            });
        }

        public void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet networkActiveHandEquipmentSet)
        {
            var unit = GetUnitEntity(networkActiveHandEquipmentSet.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to set active hand equipment set for missing unit. UnitId={UnitId}", networkActiveHandEquipmentSet.UnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                if (unit.Body.CurrentHandEquipmentSetIndex == networkActiveHandEquipmentSet.Index)
                {
                    return;
                }

                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(networkActiveHandEquipmentSet);
                var previousIndex = unit.Body.CurrentHandEquipmentSetIndex;
                unit.Body.CurrentHandEquipmentSetIndex = networkActiveHandEquipmentSet.Index;
                RefreshInventoryWindow();
                _logger.LogInformation("Changed active hand equipment slot. UnitId={UnitId}, PreviousIndex={PreviousIndex}, CurrentIndex={CurrentIndex}", networkActiveHandEquipmentSet.UnitId, previousIndex, unit.Body.CurrentHandEquipmentSetIndex);
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

        public void ApplyPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck)
        {
            var mapObject = GetMapObject(networkPerceptionCheck.MapObject.Id);
            if (mapObject == null)
            {
                _logger.LogError("Unable to apply perception check due to missing map object. MapObjectId={MapObjectId}", networkPerceptionCheck.MapObject.Id);
                return;
            }

            var unit = GetUnitEntity(networkPerceptionCheck.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to apply perception check due to missing unit. MapObjectId={MapObjectId}, UnitId={UnitId}", networkPerceptionCheck.MapObject.Id, networkPerceptionCheck.UnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Trigerring perception check. MapObjectId={MapObjectId}", networkPerceptionCheck.MapObject.Id);
                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(networkPerceptionCheck);
                PartyPerceptionController.RollPerception(unit, mapObject);
            });
        }

        public void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck)
        {
            var targetUnit = GetUnitEntity(networkInspectionKnowledgeCheck.TargetUnitId);
            if (targetUnit == null)
            {
                _logger.LogError("Unable to apply inspection knowledge check due to missing target unit. TargetUnitId={TargetUnitId}", networkInspectionKnowledgeCheck.TargetUnitId);
                return;
            }

            var initiatorUnit = GetUnitEntity(networkInspectionKnowledgeCheck.InitiatorUnitId);
            if (initiatorUnit == null)
            {
                _logger.LogError("Unable to apply inspection knowledge check due to missing initiator unit. InitiatorUnitId={InitiatorUnitId}", networkInspectionKnowledgeCheck.InitiatorUnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Applying inspection knowledge check. InitiatorUnitId={InitiatorUnitId}, TargetUnitId={TargetUnitId}, StatType={StatType}", networkInspectionKnowledgeCheck.InitiatorUnitId, networkInspectionKnowledgeCheck.TargetUnitId, networkInspectionKnowledgeCheck.StatType);
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

                var ruleSkillCheck = GameHelper.TriggerSkillCheck(new RuleSkillCheck(initiatorUnit, networkInspectionKnowledgeCheck.StatType, networkInspectionKnowledgeCheck.DC)
                {
                    IgnoreDifficultyBonusToDC = true
                }, null, true);

                info.SetCheck(ruleSkillCheck.RollResult);
                EventBus.RaiseEvent<IKnowledgeHandler>(x => x.HandleKnowledgeUpdated(info), true);
            });
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
                },

            };

            return settings;
        }

        public void ApplyGameSettings(NetworkGameSettings networkGameSettings)
        {
            if (networkGameSettings == null)
            {
                _logger.LogWarning("Game settings are null");
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Applying settings. Settings={Settings}", networkGameSettings);
                // turn based
                SettingsRoot.Game.TurnBased.EnableTurnBasedMode.SetValueAndConfirm(networkGameSettings.TurnBased.IsTurnBasedModeEnabled);
                SettingsRoot.Game.TurnBased.AutoEndTurn.SetValueAndConfirm(networkGameSettings.TurnBased.AutoEndTurn);
                SettingsRoot.Game.TurnBased.AutoStopAfterFirstMoveAction.SetValueAndConfirm(networkGameSettings.TurnBased.AutoStopAfterFirstMoveAction);
                if (networkGameSettings.TurnBased.TimeScaleInPlayerTurn.HasValue)
                {
                    SettingsRoot.Game.TurnBased.TimeScaleInPlayerTurn.SetValueAndConfirm(networkGameSettings.TurnBased.TimeScaleInPlayerTurn.Value);
                }
                if (networkGameSettings.TurnBased.TimeScaleInNonPlayerTurn.HasValue)
                {
                    SettingsRoot.Game.TurnBased.TimeScaleInNonPlayerTurn.SetValueAndConfirm(networkGameSettings.TurnBased.TimeScaleInNonPlayerTurn.Value);
                }

                // main
                SettingsRoot.Game.Main.LootInCombat.SetValueAndConfirm(networkGameSettings.Main.LootInCombat);
                SettingsRoot.Game.Main.AcceleratedMove.SetValueAndConfirm(networkGameSettings.Main.QuickMovement);

                // autopause
                SettingsRoot.Game.Autopause.ContinueMovementOnEngagement.SetValueAndConfirm(networkGameSettings.Autopause.ContinueMovementOnEngagement);
                SettingsRoot.Game.Autopause.PauseOnAllyDown.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnAllyDown);
                SettingsRoot.Game.Autopause.PauseOnAreaLoaded.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnAreaLoaded);
                SettingsRoot.Game.Autopause.PauseOnAttackOfOpportunity.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnAttackOfOpportunity);
                SettingsRoot.Game.Autopause.PauseOnEndedBuffSummon.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEndedBuffSummon);
                SettingsRoot.Game.Autopause.PauseOnEndOfPartyMembersRound.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEndOfPartyMembersRound);
                SettingsRoot.Game.Autopause.PauseOnEndOfRound.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEndOfRound);
                SettingsRoot.Game.Autopause.PauseOnEnemyDown.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEnemyDown);
                SettingsRoot.Game.Autopause.PauseOnEnemySpotted.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEnemySpotted);
                SettingsRoot.Game.Autopause.PauseOnEngagement.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEngagement);
                SettingsRoot.Game.Autopause.PauseOnHiddenObjectDetected.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnHiddenObjectDetected);
                SettingsRoot.Game.Autopause.PauseOnLostFocus.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnLostFocus);
                SettingsRoot.Game.Autopause.PauseOnLowHealth.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnLowHealth);
                SettingsRoot.Game.Autopause.PauseOnMeleeEngagement.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnMeleeEngagement);
                SettingsRoot.Game.Autopause.PauseOnNewEnemyAppeared.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnNewEnemyAppeared);
                SettingsRoot.Game.Autopause.PauseOnPartyIsAttacked.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnPartyIsAttacked);
                SettingsRoot.Game.Autopause.PauseOnPartyMemberFinishedAbility.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnPartyMemberFinishedAbility);
                SettingsRoot.Game.Autopause.PauseOnPartyMemberRanOutOfConsumable.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnPartyMemberRanOutOfConsumable);
                SettingsRoot.Game.Autopause.PauseOnSpellcastFinished.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnSpellcastFinished);
                SettingsRoot.Game.Autopause.PauseOnSpellcastInterrupted.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnSpellcastInterrupted);
                SettingsRoot.Game.Autopause.PauseOnSpellcastStarted.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnSpellcastStarted);
                SettingsRoot.Game.Autopause.PauseOnTrapDetected.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnTrapDetected);
                SettingsRoot.Game.Autopause.PauseOnWeaponIsIneffective.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnWeaponIsIneffective);
                SettingsRoot.Game.Autopause.PauseWhenAllyUnconscious.SetValueAndConfirm(networkGameSettings.Autopause.PauseWhenAllyUnconscious);
                SettingsRoot.Game.Autopause.PauseWhenEnemyUnconscious.SetValueAndConfirm(networkGameSettings.Autopause.PauseWhenEnemyUnconscious);
                SettingsRoot.Game.Autopause.PauseWhenLastSleepingEnemyStays.SetValueAndConfirm(networkGameSettings.Autopause.PauseWhenLastSleepingEnemyStays);

                // mp settings
                if (networkGameSettings.Multiplayer != null)
                {
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.Combat.AISync.Key, networkGameSettings.Multiplayer.SyncAICombatActions);
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.RemoteRollRetrievalTimeout.Key, networkGameSettings.Multiplayer.RemoteRollRetrievalTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.NetworkAwaiterTimeout.Key, networkGameSettings.Multiplayer.NetworkAwaiterTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.AISyncTimeout.Key, networkGameSettings.Multiplayer.AISyncTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.RestEncounterSyncTimeout.Key, networkGameSettings.Multiplayer.RestEncounterSyncTimeout.ToString());
                }
            });
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
            _mainThreadAccessor.Post(() =>
            {
                var campPosition = new Vector3(position.X, position.Y, position.Z);
                _logger.LogInformation("Spawning camp place. Position={Position}", position);
                RestHelper.SpawnCampPlace(campPosition);
            });
        }

        public void SetCampingUseHealingSpells(bool isOn)
        {
            _mainThreadAccessor.Post(() =>
            {
                var restView = RestView;
                if (restView == null)
                {
                    return;
                }

                restView.m_HealingToggle.isOn = isOn;

                var campingState = Game.Instance.Player.Camping;
                campingState.UseSpells = isOn;
            });
        }

        public void SetCampingState(NetworkCampingState networkCampingState)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var restView = RestView;
                    if (restView != null)
                    {
                        restView.m_AutotuneToggle.isOn = networkCampingState.AutotuneIterationsStatus;
                        restView.ViewModel?.HandleIterationsCountCalculated(networkCampingState.IterationsCount);
                    }

                    var campingState = Game.Instance.Player.Camping;

                    UpdateCookingRecipe(campingState, networkCampingState.CookingBlueprintRecipeId);
                    UpdateAlchemistRecipe(campingState, networkCampingState.PotionBlueprintRecipeId);
                    UpdateScrollScribingRecipe(campingState, networkCampingState.ScrollBlueprintRecipeId);

                    campingState.RestIterationsCount = networkCampingState.IterationsCount;
                    campingState.m_AutoTuneRestIterations = networkCampingState.AutotuneIterationsStatus;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during updating camping state");
                    throw;
                }
            });
        }

        public void SetCampingRoles(List<NetworkCampingRole> networkCampingRoles)
        {
            _mainThreadAccessor.Post(() =>
            {
                var campingState = Game.Instance.Player.Camping;
                foreach (var role in networkCampingRoles)
                {
                    campingState.CurrentCampingRoles[role.RoleType].PrimaryUnit = GetUnitEntity(role.PrimaryUnitId);
                    campingState.CurrentCampingRoles[role.RoleType].SecondaryUnit = GetUnitEntity(role.SecondaryUnitId);
                }
            });
        }

        public void UpdateGroupChangerUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (GroupChangerView == null)
                    {
                        return;
                    }

                    _logger.LogInformation("Changing group changer view state. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                    GroupChangerView.m_AcceptButton.Interactable = isInteractable;
                    GroupChangerView.m_CloseButton.Interactable = isInteractable;
                    var acceptButtonText = GroupChangerView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                    UpdateButtonTextCounter(acceptButtonText, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update group changer view state");
                    throw;
                }
            });
        }

        public void AcceptGroupChangerParty()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (GroupChangerView == null)
                    {
                        return;
                    }

                    var viewModel = GroupChangerView.ViewModel;
                    viewModel.InternalGo();
                    viewModel.m_ActionGo?.Invoke();
                    _logger.LogInformation("Group changer has been accepted");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to accept group changer");
                    throw;
                }
            });
        }

        public void ClickGroupChangerUnit(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (GroupChangerView == null)
                    {
                        return;
                    }

                    var view = GroupChangerView.m_CharacterViews.FirstOrDefault(v => string.Equals(v.UnitRef.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
                    if (view == null)
                    {
                        _logger.LogError("Unable to find character view in group manager ui. UnitId={UnitId}", unitId);
                        return;
                    }

                    var viewModel = view.ViewModel;
                    viewModel.Click.Execute(viewModel);
                    _logger.LogInformation("Executed click on group manager unit. UnitId={UnitId}", unitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to execute click for group manager unit. UnitId={UnitId}", unitId);
                    throw;
                }
            });
        }

        public void CloseGroupChangerUI()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (GroupChangerView == null)
                    {
                        return;
                    }

                    _logger.LogInformation("Closing group changer view");
                    var viewModel = GroupChangerView.ViewModel;
                    viewModel?.m_ActionClose?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to close group changer");
                    throw;
                }
            });
        }

        public void UpdateSkipTimeUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (SkipTimeView == null)
                    {
                        _logger.LogWarning("Unable to update skip time due to missing UI");
                        return;
                    }

                    _logger.LogInformation("Updating skip time ui state. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                    SkipTimeView.m_CloseButton.Interactable = isInteractable;
                    SkipTimeView.m_HoursSlider.interactable = isInteractable;
                    SkipTimeView.m_SkipTimeButton.Interactable = isInteractable;
                    var skipTimeButtonText = SkipTimeView.m_SkipTimeButton.GetComponentInChildren<TextMeshProUGUI>();
                    UpdateButtonTextCounter(skipTimeButtonText, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update skip time ui");
                    throw;
                }
            });
        }

        public void CloseSkipTimeUI()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (SkipTimeView == null)
                    {
                        return;
                    }

                    SkipTimeView.m_CloseButton.OnLeftClick.Invoke();
                    _logger.LogInformation("Skip time UI has been closed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to close Skip Time UI");
                    throw;
                }
            });
        }

        public void OpenSkipTimeUI()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (SkipTimeView != null)
                {
                    return;
                }

                EventBus.RaiseEvent<ISkipTimeWindowUIHandler>(x => x.HandleOpenSkipTime(), true);
            });
        }

        public void UpdateSkipTimeHours(float hours)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (SkipTimeView == null)
                    {
                        _logger.LogWarning("Unable to update skip time hours due to missing UI");
                        return;
                    }

                    SkipTimeView.m_HoursSlider.value = hours;
                    _logger.LogInformation("Skip time hours has been updated. Hours={Hours}", hours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update skip time hours slider");
                    throw;
                }
            });
        }

        public void StartSkipTime()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (SkipTimeView == null)
                    {
                        _logger.LogWarning("Unable to start skip time due to missing UI");
                        return;
                    }

                    SkipTimeView.m_SkipTimeButton.OnLeftClick.Invoke();
                    _logger.LogInformation("Skip time has been started");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start skip time");
                    throw;
                }
            });
        }

        public void UpdateStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (RestView == null)
                    {
                        return;
                    }

                    _logger.LogInformation("Changing rest button state. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                    RestView.m_StartRestButton.Interactable = isInteractable;
                    UpdateButtonTextCounter(RestView.m_StartRestButtonText, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update start rest button state");
                    throw;
                }
            });
        }

        public void StartRest()
        {
            _mainThreadAccessor.Post(() =>
            {
                RestView?.StartRest();
            });
        }

        public void SetRandomEncounterContext(NetworkRandomEncounterContext networkRandomEncounterContext)
        {
            _networkExecutionContext.Value = RemoteExecutionContext.Create(networkRandomEncounterContext);
        }

        public void SetGroundMoveEveryone()
        {
            _networkExecutionContext.Value = new RemoteExecutionContext
            {
                UnitsMovement = new UnitsMovementContext { ShouldMoveEveryone = true }
            };
        }

        public void UpdateIsInCombatStatus()
        {
            Game.Instance.Player.UpdateIsInCombat();
            _mainThreadAccessor.Post(() =>
            {
                Game.Instance.Player.UpdateIsInCombat();
            });
        }

        public void TryInterruptRestBanter(NetworkRestBanter networkRestBanter)
        {
            _mainThreadAccessor.Post(() =>
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
                        _logger.LogWarning("RestController is playing different banter. CurrentBanterKey={CurrentBanterKey}, CurrentSpeakerUnitId={CurrentSpeakerUnitId}, NetworkBanterKey={NetworkBanterKey}, NetworkSpeakerUnitId={NetworkSpeakerUnitId}",
                                currentBark.Text.Key, currentBark.Speaker.UniqueId, networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                        return;
                    }

                    currentBanterPlayer.InterruptBark();
                    _logger.LogInformation("Rest bark has been interrupted. NetworkBanterKey={NetworkBanterKey}, NetworkSpeakerUnitId={NetworkSpeakerUnitId}", networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to interrupd bark. NetworkBanterKey={NetworkBanterKey}, NetworkSpeakerUnitId={NetworkSpeakerUnitId}", networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                    throw;
                }
            });
        }

        public void TransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (networkVendorItemTransfer.ItemActionTarget == VendorItemActionTarget.Sell && networkVendorItemTransfer.ItemAction == VendorItemAction.Add)
                    {
                        // add for sell is different due to unsynced state of the player's inventory
                        AddItemToVendorSellCollection(networkVendorItemTransfer);
                        RefreshVendorScreen();
                        return;
                    }

                    var (item, action) = GetDataForVendorTransferAction(networkVendorItemTransfer.Item, networkVendorItemTransfer.Count, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                    if (item == null)
                    {
                        _logger.LogError("Unable to find item for make vendor transfer action. ItemId={ItemId}, ActionTarget={ActionTarget}, ActionType={ActionType}", networkVendorItemTransfer.Item.UniqueId, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                        return;
                    }

                    if (action == null)
                    {
                        _logger.LogError("Unable to find to determine correct action to make vendor transfer. ItemId={ItemId}, ActionTarget={ActionTarget}, ActionType={ActionType}", networkVendorItemTransfer.Item.UniqueId, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                        return;
                    }

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateVendorItemTransfer(item.UniqueId);
                    var transferredItem = action(item);
                    RefreshVendorScreen();
                    _logger.LogInformation("Vendor item has been transferred. ItemId={ItemId}, Count={Count}, ActionTarget={ActionTarget}, ActionType={ActionType}", transferredItem.UniqueId, transferredItem.Count, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while transfering vendor items");
                    throw;
                }
            });
        }

        public void CloseVendorWindow()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (VendorViewVM == null)
                {
                    _logger.LogWarning("Unable to close vendor screen due to missing VendorViewVM");
                    return;
                }

                VendorViewVM.m_CloseAction?.Invoke();
                Pause(false);
            });
        }

        public void MakeVendorDeal()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (VendorViewVM == null)
                {
                    _logger.LogWarning("Unable to make vendor deal due to missing VendorViewVM");
                    return;
                }

                VendorViewVM.Deal();
            });
        }

        public void ForgetSpell(NetworkSpellSlot networkSpellSlot)
        {
            var unit = GetUnitEntity(networkSpellSlot.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to find unit to forget spell. UnitId={UnitId}", networkSpellSlot.UnitId);
                return;
            }

            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkSpellSlot.SpellbookId, StringComparison.OrdinalIgnoreCase));
            if (spellbook == null)
            {
                _logger.LogError("Unable to find spellbook to forget spell. UnitId={UnitId}, SpellbookId={SpellbookId}", networkSpellSlot.UnitId, networkSpellSlot.SpellbookId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                var spellSlot = GetSpellSlot(spellbook, networkSpellSlot);
                if (spellSlot == null)
                {
                    _logger.LogError("Unable to find spellslot to forget. UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}", networkSpellSlot.UnitId, networkSpellSlot.SpellbookId, networkSpellSlot.Index, networkSpellSlot.Type);
                    return;
                }

                AddCombatText(WellKnownKeys.GameNotifications.SpellBook.ForgottenSpell.Key, spellSlot.SpellShell?.Name, unit.CharacterName);
                spellbook.ForgetMemorized(spellSlot);
                RefreshSpellbookUI();
            });
        }

        public void MemorizeSpell(NetworkSpellSlot networkSpellSlot)
        {
            var unit = GetUnitEntity(networkSpellSlot.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to find unit to memorize spell. UnitId={UnitId}", networkSpellSlot.UnitId);
                return;
            }

            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkSpellSlot.SpellbookId, StringComparison.OrdinalIgnoreCase));
            if (spellbook == null)
            {
                _logger.LogError("Unable to find spellbook to memorize spell. UnitId={UnitId}, SpellbookId={SpellbookId}", networkSpellSlot.UnitId, networkSpellSlot.SpellbookId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                var spellSlot = GetSpellSlot(spellbook, networkSpellSlot);
                var spell = GetKnownSpell(spellbook, networkSpellSlot.SpellId, networkSpellSlot.SpellName);
                spellbook.Memorize(spell, spellSlot);
                AddCombatText(WellKnownKeys.GameNotifications.SpellBook.MemorizedSpell.Key, spell.Name, unit.CharacterName);
                RefreshSpellbookUI();
            });
        }

        public void StartLeveling(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var partyView = PartyPCView?.m_Characters?.FirstOrDefault(p => string.Equals(p.UnitEntityData.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
                    if (partyView?.ViewModel == null)
                    {
                        _logger.LogError("Unable to start leveling due to missing party character vm. UnitId={UnitId}", unitId);
                        return;
                    }

                    _logger.LogInformation("Starting leveling process. UnitId={UnitId}", unitId);
                    partyView.ViewModel.LevelUp();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while starting leveling process. UnitId={UnitId}", unitId);
                    throw;
                }
            });
        }

        public void SelectLevelingClassArchetype(string archetypeId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (CharGenView == null)
                    {
                        _logger.LogWarning("Can't select class archetype due to missing CharGenView");
                        return;
                    }

                    var viewModel = GetLevelingPhaseViewModel();
                    if (viewModel == null)
                    {
                        _logger.LogError("Unable to get leveling phase viewmodel");
                        return;
                    }

                    if (string.IsNullOrEmpty(archetypeId))
                    {
                        viewModel.SelectedClassVM.Value.TryUnselectArchetypes();
                        viewModel.OnSelectorArchetypeChanged(null);
                        return;
                    }

                    if (viewModel.SelectedClassVM.Value == null)
                    {
                        _logger.LogWarning("Class must be selected to select archetype");
                        return;
                    }

                    var archetypes = viewModel.SelectedClassVM.Value.GetArchetypesList(viewModel.SelectedClassVM.Value.Class).Cast<CharGenClassSelectorItemVM>().ToList();
                    var archetype = archetypes.FirstOrDefault(c => string.Equals(c.Archetype.AssetGuid.ToString(), archetypeId, StringComparison.OrdinalIgnoreCase));
                    if (archetype == null)
                    {
                        ShowWarningNotification(WellKnownKeys.GameNotifications.Leveling.ArchetypeContentMismatch.Key);
                        return;
                    }

                    archetype.IsSelected.Value = true;
                    viewModel.SelectedClassVM.Value.SelectedArchetype.Value = archetype;
                    viewModel.OnSelectorArchetypeChanged(archetype.Archetype);
                    viewModel.LastSelectedArchetypeVM = archetype;
                    _logger.LogInformation("Leveling archetype has been set. ClassName={ClassName}, ArchetypeId={ArchetypeId}", viewModel.SelectedClassVM.Value.Class.NameForAcronym, viewModel.SelectedClassVM.Value.SelectedArchetype.Value?.Archetype?.NameForAcronym);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while selecting leveling class archetype. ArchetypeId={ArchetypeId}", archetypeId);
                    throw;
                }
            });
        }

        public void SelectLevelingClass(string classId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (CharGenView == null)
                    {
                        _logger.LogWarning("Can't select class archetype due to missing CharGenView");
                        return;
                    }

                    var viewModel = GetLevelingPhaseViewModel();
                    if (viewModel == null)
                    {
                        _logger.LogError("Unable to get leveling phase viewmodel");
                        return;
                    }

                    var selectedClass = viewModel.m_ClassesVMs.FirstOrDefault(c => string.Equals(c.Class.AssetGuid.ToString(), classId, StringComparison.OrdinalIgnoreCase));
                    viewModel.SelectedClassVM.Value = selectedClass;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while selecting leveling class. ClassId={ClassId}", classId);
                    throw;
                }
            });
        }

        public void SelectLevelingFeature(NetworkLevelingFeature networkLevelingFeature)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (CharGenView == null)
                    {
                        _logger.LogWarning("Can't select feature due to missing CharGenView");
                        return;
                    }

                    var view = CharGenView?.SelectedDetailView as CharGenFeatureSelectorPhaseDetailedPCView;
                    if (view == null)
                    {
                        _logger.LogError("Unable to get leveling feature view");
                        return;
                    }

                    var featureToSelect = view.m_Selector.VirtualList.Elements.FirstOrDefault(x => x.Data is CharGenFeatureSelectorItemVM featureItem
                         && string.Equals(featureItem.Feature.NameForAcronym, networkLevelingFeature.Name, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(featureItem.Feature.Feature.AssetGuid.ToString(), networkLevelingFeature.Id, StringComparison.OrdinalIgnoreCase));
                    if (featureToSelect == null)
                    {
                        _logger.LogError("Unable to find requested feature in the list. FeatureName={FeatureName}, FeatureId={FeatureId}", networkLevelingFeature.Name, networkLevelingFeature.Id);
                        return;
                    }

                    var requestedFeatureVM = (featureToSelect.Data as CharGenFeatureSelectorItemVM);
                    requestedFeatureVM.SetSelected(true);
                    _logger.LogInformation("Selected leveling feature. FeatureName={FeatureName}, FeatureId={FeatureId}", requestedFeatureVM.Feature.NameForAcronym, requestedFeatureVM.Feature.Feature.AssetGuid.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while selecting leveling feature. FeatureName={FeatureName}, FeatureId={FeatureId}", networkLevelingFeature.Name, networkLevelingFeature.Id);
                    throw;
                }
            });
        }

        public void UpdateLevelingPhaseControls(bool isEnabled)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (CharGenView == null)
                    {
                        _logger.LogError("Unable to update leveling controls due too missing CharGenView");
                        return;
                    }

                    _logger.LogInformation("Updating generic part of leveling screen. IsEnabled={IsEnabled}", isEnabled);
                    CharGenView.m_CloseButton.Interactable = isEnabled;
                    CharGenView.SetActiveNextPhaseButton(isEnabled);

                    var nextEnabled = CharGenView.CanGoNext.Value && isEnabled;
                    CharGenView.m_NextButton.Interactable = nextEnabled;
                    CharGenView.m_NextValidPageButton.Interactable = nextEnabled;
                    var backEnabled = CharGenView.CanGoBack.Value && isEnabled;
                    CharGenView.m_BackButton.Interactable = backEnabled;
                    CharGenView.m_FirstPageButton.Interactable = backEnabled;

                    foreach (var roadmapPhase in CharGenView.RoadmapMenuView.m_VisiblePhases)
                    {
                        var baseView = roadmapPhase as CharGenPhaseRoadmapBaseView<CharGenPhaseBaseVM>;
                        if (baseView != null)
                        {
                            baseView.m_Button.Interactable = baseView.m_Button.Interactable && isEnabled;
                            baseView.m_ButtonBackground.Interactable = baseView.m_ButtonBackground.Interactable && isEnabled;
                            baseView.m_ButtonLabel.Interactable = baseView.m_ButtonLabel.Interactable && isEnabled;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating leveling phase controls");
                    throw;
                }
            });
        }

        public void SwitchLevelingPhase(NetworkLevelingPhase networkLevelingPhase)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (CharGenView == null)
                {
                    _logger.LogError("Unable to update switch leveling phase due too missing CharGenView");
                    return;
                }

                var roadmapVM = CharGenView.RoadmapMenuView.ViewModel;
                if (networkLevelingPhase.Index >= roadmapVM.EntitiesCollection.Count)
                {
                    _logger.LogError("Leveling phase is out of range. Index={Index}, TotalCount={TotalCount}", networkLevelingPhase.Index, roadmapVM.EntitiesCollection.Count);
                    return;
                }

                var phaseVM = roadmapVM.EntitiesCollection[networkLevelingPhase.Index];
                roadmapVM.SelectedEntity.Value = phaseVM;
            });
        }

        public void SelectLevelingSpell(NetworkLevelingSpell networkLevelingSpell)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var spellsPhaseVM = GetCharGenSpellsPhaseVM();
                    if (spellsPhaseVM == null)
                    {
                        return;
                    }

                    var spellToAdd = spellsPhaseVM.SpellsSelector.Value.EntitiesCollection.FirstOrDefault(x => string.Equals(x.Spell.AssetGuid.ToString(), networkLevelingSpell.Id, StringComparison.OrdinalIgnoreCase));
                    if (spellToAdd == null)
                    {
                        _logger.LogError("Unable to add missing leveling spell. SpellName={SpellName}, SpellId={SpellId}", networkLevelingSpell.Name, networkLevelingSpell.Id);
                        return;
                    }

                    spellsPhaseVM.SpellsSelector.Value.SelectedEntitiesCollection.Add(spellToAdd);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while selecting leveling spell. SpellName={SpellName}, SpellId={SpellId}", networkLevelingSpell.Name, networkLevelingSpell.Id);
                    throw;
                }
            });
        }

        public void RemoveLevelingSpell(NetworkLevelingSpell networkLevelingSpell)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var spellsPhaseVM = GetCharGenSpellsPhaseVM();
                    if (spellsPhaseVM == null)
                    {
                        return;
                    }

                    var spellToRemove = spellsPhaseVM.SpellsSelector.Value.SelectedEntitiesCollection.FirstOrDefault(x => string.Equals(x.Spell.AssetGuid.ToString(), networkLevelingSpell.Id, StringComparison.OrdinalIgnoreCase));
                    if (spellToRemove == null)
                    {
                        _logger.LogError("Unable to remove missing leveling spell. SpellName={SpellName}, SpellId={SpellId}", networkLevelingSpell.Name, networkLevelingSpell.Id);
                        return;
                    }

                    spellsPhaseVM.SpellsSelector.Value.SelectedEntitiesCollection.Remove(spellToRemove);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while removing selected leveling spell. SpellName={SpellName}, SpellId={SpellId}", networkLevelingSpell.Name, networkLevelingSpell.Id);
                    throw;
                }
            });
        }

        public void IncreaseLevelingSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var skillView = GetLevelingSkillAllocatorView(networkLevelingSkillPoint.StatType);
                    if (skillView == null)
                    {
                        return;
                    }

                    skillView.ViewModel.m_LevelUpController.SpendSkillPoint(skillView.ViewModel.StatType);
                    skillView.OnChangedValue();
                    _logger.LogInformation("Leveling skillpoint has been increased. StatType={StatType}", networkLevelingSkillPoint.StatType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while increasing leveling skill point. StatType={StatType}", networkLevelingSkillPoint.StatType);
                    throw;
                }
            });
        }

        public void DecreaseLevelingSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var skillView = GetLevelingSkillAllocatorView(networkLevelingSkillPoint.StatType);
                    if (skillView == null)
                    {
                        return;
                    }
                    skillView.ViewModel.m_LevelUpController.UnspendSkillPoint(skillView.ViewModel.StatType);
                    skillView.OnChangedValue();
                    _logger.LogInformation("Leveling skillpoint has been decreased. StatType={StatType}", networkLevelingSkillPoint.StatType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while decreasing leveling skill point. StatType={StatType}", networkLevelingSkillPoint.StatType);
                    throw;
                }
            });
        }

        public void IncreaseLevelingAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var abilityScoreView = GetLevelingAbilityScoreAllocatorView(networkLevelingAbilityScore.StatType);
                    if (abilityScoreView == null)
                    {
                        return;
                    }

                    abilityScoreView.ViewModel.m_LevelUpController.SpendSkillPoint(abilityScoreView.ViewModel.StatType);
                    abilityScoreView.OnChangedValue();
                    _logger.LogInformation("Leveling ability score has been increased. StatType={StatType}", networkLevelingAbilityScore.StatType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while increasing leveling ability score. StatType={StatType}", networkLevelingAbilityScore.StatType);
                    throw;
                }
            });
        }

        public void DecreaseLevelingAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var abilityScoreView = GetLevelingAbilityScoreAllocatorView(networkLevelingAbilityScore.StatType);
                    if (abilityScoreView == null)
                    {
                        return;
                    }
                    abilityScoreView.ViewModel.m_LevelUpController.UnspendSkillPoint(abilityScoreView.ViewModel.StatType);
                    abilityScoreView.OnChangedValue();
                    _logger.LogInformation("Leveling ability score has been decreased. StatType={StatType}", networkLevelingAbilityScore.StatType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while decreasing leveling ability score. StatType={StatType}", networkLevelingAbilityScore.StatType);
                    throw;
                }
            });
        }

        public void CompleteLeveling()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    CharGenView.ViewModel.Complete();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while completing char gen");
                    throw;
                }
            });
        }

        public void TerminateLeveling()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    CharGenView.ViewModel.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing char gen");
                    throw;
                }
            });
        }

        public void MoveActionBarSlots(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var unit = GetUnitEntity(sourceActionBarSlot.UnitId);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to move action bar slot for missing unit. UnitId={UnitId}", sourceActionBarSlot.UnitId);
                        return;
                    }

                    if (targetActionBarSlot.Index == -1)
                    {
                        return;
                    }

                    if (sourceActionBarSlot.Index == -1)
                    {
                        // adding from spells/abilities/items panel
                        var slot = LoadActionBarSlot(unit, sourceActionBarSlot);
                        if (slot == null)
                        {
                            _logger.LogError("Unable to load action bar slot from existing spells/abilities/items. UnitId={UnitId}, SourceSlotIndex={SourceSlotIndex}", sourceActionBarSlot.Index);
                            return;
                        }

                        unit.UISettings.SetSlot(slot, targetActionBarSlot.Index);
                        return;
                    }

                    var sourceSlot = unit.UISettings.GetSlot(sourceActionBarSlot.Index, unit);
                    var targetSlot = unit.UISettings.GetSlot(targetActionBarSlot.Index, unit);
                    unit.UISettings.SetSlot(sourceSlot, targetActionBarSlot.Index);
                    unit.UISettings.SetSlot(targetSlot, sourceActionBarSlot.Index);

                    _logger.LogInformation("Action bar slots have been updated for unit. UnitId={UnitId}", targetActionBarSlot.UnitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to move action bar slots");
                    throw;
                }
            });
        }

        public void ClearActionBarSlot(NetworkActionBarSlot actionBarSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = GetUnitEntity(actionBarSlot.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to clear action bar slot for missing unit. UnitId={UnitId}", actionBarSlot.UnitId);
                    return;
                }

                if (actionBarSlot.Index == -1)
                {
                    return;
                }

                var emptySlot = new MechanicActionBarSlotEmpty();
                unit.UISettings.SetSlot(emptySlot, actionBarSlot.Index);
            });
        }

        public void LockpickMapObject(NetworkLockpickInteraction lockpickInteraction)
        {
            _mainThreadAccessor.Post(() =>
            {
                var mapObject = GetMapObject(lockpickInteraction.MapObject.Id);
                List<UnitEntityData> units = [.. lockpickInteraction.Units.Select(GetUnitEntity)];

                using var lockpickVM = new LockpickVM(mapObject.View, null);
                using var context = _networkExecutionContext.Value = new RemoteExecutionContext
                {
                    SelectedUnits = units,
                    LockpickContext = new MapObjectLockpickContext { MapObjectId = lockpickInteraction.MapObject.Id }
                };
                lockpickVM.OnInteraction(lockpickInteraction.LockpickType);
            });
        }

        public void AttackUnit(NetworkUnitAttack attack)
        {
            _mainThreadAccessor.Post(() =>
            {
                var executor = GetUnitEntity(attack.ExecutorUnitId);
                if (executor == null)
                {
                    _logger.LogError("Unable to find executor unit to perform unit attack command. ExecutorUnitId={ExecutorUnitId}", attack.ExecutorUnitId);
                    return;
                }
                var target = GetUnitEntity(attack.TargetUnitId);
                if (target == null)
                {
                    _logger.LogError("Unable to find target unit to perform unit attack command. TargetUnitId={TargetUnitId}", attack.TargetUnitId);
                    return;
                }

                var command = UnitAttack.CreateAttackCommand(executor, target) as UnitAttack;
                if (attack.IsFullAttack)
                {
                    command.ForceFullAttack = true;
                }

                var movementPath = attack.VectorPath.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
                command.ForcedPath = new ForcedPath(movementPath);
                command.CreatedByPlayer = true;
                _logger.LogInformation("Starting unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, ForceFullAttack={ForceFullAttack}", attack.ExecutorUnitId, attack.TargetUnitId, attack.IsFullAttack);
                executor.Commands.Run(command);
            });
        }

        public void DelayCombatTurn(string unitId, string targetUnitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to delay combat turn due to missing unit. UnitId={UnitId}", unitId);
                    return;
                }

                var targetUnit = GetUnitEntity(targetUnitId);
                if (targetUnit == null)
                {
                    _logger.LogError("Unable to delay combat turn due to missing target unit. TargetUnitId={TargetUnitId}", targetUnit);
                    return;
                }

                Game.Instance.TurnBasedCombatController.HandleDelayTurn(unit, targetUnit);
            });
        }

        public void ChangeUnitStealth(string unitId, bool isEnabled, bool isForced)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to change stealth due to missing unit. UnitId={UnitId}", unitId);
                    return;
                }

                unit.Stealth.WantEnterStealth = isEnabled;
                unit.Stealth.ForceEnterStealth = isForced;
                Game.Instance.StealthController.TickUnit(unit);

                _logger.LogInformation("Unit stealth has been changed. UnitId={UnitId}, IsEnabled={IsEnabled}, IsForced={IsForced}", unitId, isEnabled, isForced);
            });
        }

        public void OpenGlobalMapRestMenu()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (RestView != null)
                {
                    return;
                }

                var isOk = RestHelper.TryStartRest();
                if (!isOk)
                {
                    _logger.LogError("Unable to start global map rest");
                    return;
                }

                _logger.LogInformation("Opened global map rest menu");
            });
        }

        public void StartGlobalMapTravel(NetworkGlobalMapLocation destination)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = GetGlobalMapPoint(destination.Id);
                if (point == null)
                {
                    _logger.LogError("Unable to find global map point. PointId={PointId}, PointName={PointName}", destination.Id, destination.Name);
                    return;
                }

                var traveler = Game.Instance.GlobalMapController.SelectedTraveler;
                GlobalMapTravelData globalMapTravelData = GlobalMapView.Instance.State.PathManager.CalculateTravelerPathToLocation(traveler, point.Blueprint);
                traveler.StartTravel(globalMapTravelData, true);
                _logger.LogInformation("Global map traveler has been started. Destination={DestinationId}, DestinationName={DestinationName}", point.Blueprint.AssetGuid.ToString(), point.name);
            });
        }

        public bool IsAtGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var targetPoint = GetGlobalMapPoint(globalMapLocation.Id);
            return targetPoint != null && GlobalMapView.Instance.State.Player.Location == targetPoint.Blueprint;
        }

        public void ContinueGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateGlobalMapState(globalMapState);

                GlobalMapUI.Instance.OnContinue();
            });
        }

        public void StopGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateGlobalMapState(globalMapState);

                GlobalMapUI.Instance.OnStop();
            });
        }

        public void UpdateGlobalMapMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (GlobalMapPCView == null)
                {
                    return;
                }

                var messageBoxView = GlobalMapPCView.m_GlobalMapEnterMessagePCView;
                if (messageBoxView?.ViewModel == null)
                {
                    return;
                }

                messageBoxView.m_AcceptButton.Interactable = !messageBoxView.ViewModel.IsCurrentLocation || isInteractable;

                var buttonText = messageBoxView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                if (messageBoxView.ViewModel.IsCurrentLocation)
                {
                    UpdateButtonTextCounter(buttonText, readyPlayersCount, totalPlayersCount);
                }
                else
                {
                    RemoveButtonTextCounter(buttonText);
                }

                _logger.LogInformation("Global Map Message box accept button has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateGlobalMapIngredientCollectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (GlobalMapPCView == null)
                {
                    return;
                }

                var modalMessage = (Game.Instance.RootUiContext.m_CommonView as CommonPCView).m_MessageModalPCView;
                modalMessage.m_AcceptButton.Interactable = isInteractable;
                var buttonText = modalMessage.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                UpdateButtonTextCounter(buttonText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Global Map Ingredient Collection Accept button has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateGlobalMapEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (GlobalMapPCView == null)
                {
                    return;
                }

                var modalMessage = GlobalMapPCView.m_GlobalMapRandomEncounterPCView;
                modalMessage.m_AvoidButton.Interactable = isInteractable;
                modalMessage.m_ContinueButton.Interactable = isInteractable;
                modalMessage.m_EnterButton.Interactable = isInteractable;

                UpdateButtonTextCounter(modalMessage.m_AvoidLabel, readyPlayersCount, totalPlayersCount);
                UpdateButtonTextCounter(modalMessage.m_ContinueLabel, readyPlayersCount, totalPlayersCount);
                UpdateButtonTextCounter(modalMessage.m_EnterLabel, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Global Map Encounter Message has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CollectGlobalMapIngredients(NetworkGlobalMapLocation globalMapLocation)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (GlobalMapPCView == null)
                {
                    return;
                }

                var modalMessage = (Game.Instance.RootUiContext.m_CommonView as CommonPCView)?.m_MessageModalPCView;
                if (modalMessage != null)
                {
                    modalMessage.m_AcceptButton.OnLeftClick?.Invoke();
                    _logger.LogInformation("Global map ingredients have been collected via Accept button");
                    return;
                }

                // no message box means client closed his message box right before host clicked accept
                // autocollecting items since we are at the same place anyway
                var point = GetGlobalMapPoint(globalMapLocation.Id);
                if (point == null)
                {
                    _logger.LogError("Unable to autocollect global map ingredients due to missing point. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
                    return;
                }

                var craftRoot = BlueprintRoot.Instance.CraftRoot.CollectRoot;
                var collected = craftRoot.CollectIngredient(point.Blueprint);
                var warningMessage = collected.Count > 0 ? craftRoot.SuccessCollect : craftRoot.FailCollected;
                UIUtility.SendWarning(warningMessage, addLog: false);
                EventBus.RaiseEvent<ILogMessageUIHandler>(x => x.HandleLogMessage((collected.Count > 0) ? $"{craftRoot.SuccessCollect}:\n{BlueprintGlobalMapPoint.IngredientToString(collected)}" : ((string)craftRoot.FailCollected)));
                point.State.IngredientWasCollected = true;
                point.State.SetVisited();

                _logger.LogInformation("Global map ingredients have been collected via auto collection. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
            });
        }

        public void EnterGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = GetGlobalMapPoint(globalMapLocation.Id);
                if (point == null || GlobalMapView.Instance.State.Player.Location != point)
                {
                    _logger.LogError("Unable to enter desynced global map location. ExpectedLocationId={ExpectedLocationId}, ExpectedLocationName={ExpectedLocationName}, ActualLocationId={ActualLocationId}, ActualLocationName={ActualLocationName}", globalMapLocation.Id, globalMapLocation.Name, point?.Blueprint.AssetGuid.ToString(), point?.Blueprint.name);
                    return;
                }

                GlobalMapPCView?.m_GlobalMapEnterMessagePCView?.ViewModel?.Close();

                GlobalMapView.Instance.EnterLocation();
                _logger.LogInformation("Global map location has been entered. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
            });
        }

        public void AvoidGlobalMapEncounter()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (GlobalMapPCView == null)
                {
                    _logger.LogWarning("Unable to avoid global map encounter when global map view is not available");
                    return;
                }

                GlobalMapPCView.m_GlobalMapRandomEncounterPCView.ViewModel.Avoid();
                _logger.LogInformation("Global map encounter has been avoided");
            });
        }

        public void AcceptGlobalMapEncounter()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (GlobalMapPCView == null)
                {
                    _logger.LogWarning("Unable to avoid global map encounter when global map view is not available");
                    return;
                }

                GlobalMapPCView.m_GlobalMapRandomEncounterPCView.ViewModel.Accept();
                _logger.LogInformation("Global map encounter has been accepted");
            });
        }

        public void RollGlobalMapEncounter(NetworkGlobalMapEncounter globalMapEncounter)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var encounter = BlueprintRoot.Instance.RE.Encounters.FirstOrDefault(e => string.Equals(e.AssetGuid.ToString(), globalMapEncounter.BlueprintId));
                    if (encounter == null)
                    {
                        _logger.LogError("Unable to find global map encounter. BlueprintId={BlueprintId}", globalMapEncounter.BlueprintId);
                        return;
                    }

                    var position = new Vector3(globalMapEncounter.Position.X, globalMapEncounter.Position.Y, globalMapEncounter.Position.Z);
                    var combatRandomEncounterData = new CombatRandomEncounterData(encounter, position)
                    {
                        IsTraderRE = globalMapEncounter.IsTrader,
                        AvoidanceCheckResult = (RandomEncounterAvoidanceCheckResult)Enum.Parse(typeof(RandomEncounterAvoidanceCheckResult), globalMapEncounter.AvoidanceResult, true)
                    };
                    var component = encounter.AreaEntrance.Area.GetComponent<CombatRandomEncounterAreaSettings>();
                    combatRandomEncounterData.RandomCombat = CombatRandomEncountersGenerator.GenerateRandomEncounter(globalMapEncounter.Seed, GlobalMapView.Instance.CurrentZone, component, null);
                    RandomEncountersController.State.Player.StartEncounter(combatRandomEncounterData);
                    Game.Instance.RandomEncountersController.m_LastStartedEncounter = RandomEncountersController.State.Player.CurrentEncounterData;
                    Game.Instance.Player.REManager.OnGlobalMapEncounterStarted(encounter, false);
                    _logger.LogInformation("Global map encounter has been rolled. BlueprintId={BlueprintId}, Seed={Seed}, AvoidanceResult={AvoidanceResult}", globalMapEncounter.BlueprintId, globalMapEncounter.Seed, globalMapEncounter.AvoidanceResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while rolling global map encounter. BlueprintId={BlueprintId}, Seed={Seed}, AvoidanceResult={AvoidanceResult}", globalMapEncounter.BlueprintId, globalMapEncounter.Seed, globalMapEncounter.AvoidanceResult);
                    throw;
                }
            });
        }

        private string GetLocalizedText(string messageKey, params object[] args)
        {
            var localizedPart = new LocalizedString { Key = messageKey };
            var message = string.Format(localizedPart, args);
            return message;
        }

        private void UpdateGlobalMapState(NetworkGlobalMapState globalMapState)
        {
            // not sure if player position is unavailable while army is selected (act2+), need to check later
            if (globalMapState.Player?.Position != null)
            {
                GlobalMapView.Instance.State.Player.TravelData.EdgePosition = globalMapState.Player.Position.Edge;
            }
        }

        private GlobalMapPointView GetGlobalMapPoint(string pointId)
        {
            var point = GlobalMapView.Instance.Points.FirstOrDefault(p => string.Equals(p.Blueprint.AssetGuid.ToString(), pointId, StringComparison.OrdinalIgnoreCase));
            return point;
        }

        private void UpdateButtonTextCounter(TextMeshProUGUI buttonText, int readyPlayersCount, int totalPlayersCount)
        {
            var baseText = GetButtonTextWithoutCounter(buttonText);
            baseText += $" ({readyPlayersCount}/{totalPlayersCount})";
            buttonText.SetText(baseText);
        }

        private void RemoveButtonTextCounter(TextMeshProUGUI buttonText)
        {
            var baseText = GetButtonTextWithoutCounter(buttonText);
            buttonText.SetText(baseText);
        }

        private string GetButtonTextWithoutCounter(TextMeshProUGUI buttonText)
        {
            var baseText = buttonText.text;
            if (baseText.EndsWith(")"))
            {
                var parts = baseText.Split(' ');
                baseText = string.Join(" ", parts.Take(parts.Length - 1));
            }

            return baseText;
        }

        private List<NetworkUnit> GetUnitsInCombat()
        {
            var unitsInCombat = Game.Instance.State.Units.InCombat().ToList();

            switch (Game.Instance.CurrentlyLoadedArea.name)
            {
                case "Prologue_Caves_1":
                    var anevia = Game.Instance.State.Units.FirstOrDefault(u => u.CharacterName == "Anevia");
                    if (anevia != null)
                    {
                        // Anevia, constantly joins midfight
                        unitsInCombat.Add(anevia);
                    }
                    break;
                default:
                    break;
            }

            var units = new List<NetworkUnit>();

            foreach (var combatUnit in unitsInCombat)
            {
                var unit = new NetworkUnit
                {
                    Id = combatUnit.UniqueId,
                    Position = new NetworkVector3(combatUnit.Position.x, combatUnit.Position.y, combatUnit.Position.z),
                    Orientation = combatUnit.Orientation,
                    TurnBasedInfo = GetUnitTurnBasedInfo(combatUnit),
                    CombatState = GetUnitCombatState(combatUnit),
                };
                units.Add(unit);
            }

            return units;
        }

        private NetworkUnitTurnBasedInfo GetUnitTurnBasedInfo(UnitEntityData combatUnit)
        {
            var unitInfo = Game.Instance.TurnBasedCombatController.FindUnitInfo(combatUnit);
            if (unitInfo == null)
            {
                return null;
            }

            var info = new NetworkUnitTurnBasedInfo
            {
                ActingInSurpriseRound = unitInfo.ActingInSurpriseRound,
                Surprising = unitInfo.Surprising,
                Surprised = unitInfo.Surprised,
            };

            return info;
        }

        private NetworkUnitCombatState GetUnitCombatState(UnitEntityData combatUnit)
        {
            if (combatUnit.CombatState == null)
            {
                return null;
            }

            var state = new NetworkUnitCombatState
            {
                EngagedUnits = [.. combatUnit.CombatState.EngagedUnits.Select(x => x.UniqueId)],
                EngagedBy = [.. combatUnit.CombatState.EngagedBy.Select(x => x.UniqueId)],
            };

            return state;
        }

        private void UpdateCombatState(NetworkCombatState networkCombatState, bool requiresFullUpdate)
        {
            try
            {
                var unitsToUpdate = networkCombatState.Units.ToDictionary(x => x, x => GetUnitEntity(x.Id));
                foreach (var (networkUnit, unit) in unitsToUpdate)
                {
                    if (unit == null)
                    {
                        _logger.LogError("Unable to update combat state for missing unit. UnitId={UnitId}", networkUnit.Id);
                        continue;
                    }

                    UpdateUnitPosition(unit, networkUnit);

                    if (requiresFullUpdate)
                    {
                        UpdateUnitTurnBasedInfo(unit, networkUnit.TurnBasedInfo);
                    }
                }

                UpdateCombatUnitState(unitsToUpdate);

                _logger.LogInformation("Finished updating combat state. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update combat state. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
                throw;
            }
        }

        private void UpdateUnitTurnBasedInfo(UnitEntityData unit, NetworkUnitTurnBasedInfo networkUnitTurnBasedInfo)
        {
            var turnBasedInfo = Game.Instance.TurnBasedCombatController.FindUnitInfo(unit);

            if (turnBasedInfo == null || networkUnitTurnBasedInfo == null)
            {
                _logger.LogWarning("Unable to update missing turn based combat info. UnitId={UnitId}", unit.UniqueId);
                return;
            }

            turnBasedInfo.Surprising = networkUnitTurnBasedInfo.Surprising;
            turnBasedInfo.Surprised = networkUnitTurnBasedInfo.Surprised;
            turnBasedInfo.ActingInSurpriseRound = networkUnitTurnBasedInfo.ActingInSurpriseRound;
        }

        private void UpdateCombatUnitState(Dictionary<NetworkUnit, UnitEntityData> unitsToUpdate)
        {
            // engagement is configured for units pair so we need to clear existing lists before syncing
            foreach (var (_, unit) in unitsToUpdate)
            {
                if (unit?.CombatState == null)
                {
                    continue;
                }

                unit.CombatState.m_EngagedBy.Clear();
                unit.CombatState.m_EngagedUnits.Clear();
            }

            foreach (var (networkUnit, unit) in unitsToUpdate)
            {
                if (unit?.CombatState == null || networkUnit.CombatState == null)
                {
                    _logger.LogInformation("Unable to update missing combat unit state. UnitId={UnitId}", networkUnit.Id);
                    continue;
                }

                foreach (var engageTargetId in networkUnit.CombatState.EngagedUnits)
                {
                    var engageTarget = GetUnitEntity(engageTargetId);
                    if (engageTarget == null)
                    {
                        _logger.LogInformation("Unable to engage missing unit. UnitId={UnitId}, EngageTargetId={EngageTargetId}", unit.UniqueId, engageTargetId);
                        continue;
                    }

                    unit.CombatState.Engage(engageTarget);
                }
            }
        }

        private void UpdateUnitPosition(UnitEntityData unit, NetworkUnit networkUnit)
        {
            if (!unit.IsInCombat)
            {
                _logger.LogWarning("Updating unit outside of the combat. UnitId={UnitId}", networkUnit.Id);
            }

            if (unit.Orientation != networkUnit.Orientation)
            {
                var previousOrientation = unit.Orientation;
                _logger.LogInformation("Orientation has been updated. UnitId={UnitId}, PreviousOrientation={PreviousOrientation}, NewOrientation={NewOrientation}", unit.UniqueId, previousOrientation.ToString("F4"), unit.Orientation.ToString("F4"));
                unit.Orientation = networkUnit.Orientation;
            }

            if (unit.Position.x != networkUnit.Position.X
                || unit.Position.y != networkUnit.Position.Y
                || unit.Position.z != networkUnit.Position.Z)
            {
                var newPosition = new Vector3(networkUnit.Position.X, networkUnit.Position.Y, networkUnit.Position.Z);
                _logger.LogInformation("Updating unit position. UnitId={UnitId}, PreviousPosition={PreviousPosition}, NewPosition={NewPosition}", unit.UniqueId, unit.Position.ToString("F4"), newPosition.ToString("F4"));
                unit.Translocate(newPosition, unit.Orientation);
            }
        }

        private IEnumerable<ItemsCollection> GetLootContainers(NetworkLootContainer networkLootContainer)
        {
            if (networkLootContainer.IsUnit)
            {
                var unit = GetUnitEntity(networkLootContainer.Id);
                if (unit == null)
                {
                    _logger.LogError("Unable to find unit to loot. UnitId={UnitId}", networkLootContainer.Id);
                    return [];
                }

                return [unit.Inventory];
            }

            var mapObject = GetMapObject(networkLootContainer.Id);
            var lookupTargets = mapObject != null ? [mapObject]
                : GetNeareastLootableMapObjects(networkLootContainer.Position);

            var mapObjectContainers = lookupTargets.Select(x => ((InteractionLootPart)x.Interactions.FindOrDefault(i => i is InteractionLootPart)).Loot);
            return mapObjectContainers;
        }

        private MechanicActionBarSlot LoadActionBarSlot(UnitEntityData unit, NetworkActionBarSlot networkActionBarSlot)
        {
            if (networkActionBarSlot.ActivatableAbility != null)
            {
                var activatableAbility = FindActivatableAbility(unit, networkActionBarSlot.ActivatableAbility);
                if (activatableAbility == null)
                {
                    _logger.LogError("Unable to find activatable ability slot content. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", unit.UniqueId, networkActionBarSlot.ActivatableAbility.Id, networkActionBarSlot.ActivatableAbility.Name);
                    return null;
                }

                var activatableAbilitySlot = new MechanicActionBarSlotActivableAbility { ActivatableAbility = activatableAbility, Unit = unit };
                return activatableAbilitySlot;
            }

            if (networkActionBarSlot.Item != null)
            {
                var itemSlot = unit.Body.QuickSlots.FirstOrDefault(s => IsSameItem(s.Item, networkActionBarSlot.Item));
                if (itemSlot == null)
                {
                    _logger.LogError("Unable to find item slot content. UnitId={UnitId}, ItemId={ItemId}, ItemName={ItemName}", unit.UniqueId, networkActionBarSlot.Item.UniqueId, networkActionBarSlot.Item.Name);
                    return null;
                }

                var itemActionBarSlot = new MechanicActionBarSlotItem { Item = itemSlot.Item, Unit = unit };
                return itemActionBarSlot;
            }

            var ability = FindAbility(unit, networkActionBarSlot.Ability);
            if (ability == null)
            {
                _logger.LogError("Unable to find ability/spell slot content. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", unit.UniqueId, networkActionBarSlot.Ability.Id, networkActionBarSlot.Ability.Name);
                return null;
            }

            if (ability.Spellbook == null)
            {
                var abilitySpellSlot = new MechanicActionBarSlotAbility { Ability = ability, Unit = unit };
                return abilitySpellSlot;
            }

            if (ability.SpellLevel == 0)
            {
                if (!string.IsNullOrEmpty(networkActionBarSlot.Ability.ConvertedFromId))
                {
                    var convertedSpellSlot = new MechanicActionBarSlotSpontaneusConvertedSpell { Spell = ability, Unit = unit };
                    return convertedSpellSlot;
                }

                var cantripSpellSlot = new MechanicActionBarSlotSpontaneousSpell(ability) { Unit = unit };
                return cantripSpellSlot;
            }

            var spellSlot = new MechanicActionBarSlotMemorizedSpell(ability.SpellSlot) { Unit = unit };
            return spellSlot;
        }

        private CharGenSkillAllocatorPCView GetLevelingSkillAllocatorView(StatType statType)
        {
            if (CharGenView == null)
            {
                _logger.LogError("Unable to get leveling skillpoint vm due too missing CharGenView");
                return null;
            }

            if (CharGenView.SelectedDetailView is not CharGenSkillsPhaseDetailedPCView skillAllocator)
            {
                _logger.LogWarning("Unable to get leveling skillpoint vm because current phase is not skill phase");
                return null;
            }

            var skillView = skillAllocator.m_StatAllocators.FirstOrDefault(x => x.ViewModel.StatType == statType);
            if (skillView == null)
            {
                _logger.LogWarning("Unable to find leveling view for stat. StatType={StatType}", statType);
                return null;
            }

            return skillView;
        }

        private CharGenAbilityScoreAllocatorPCView GetLevelingAbilityScoreAllocatorView(StatType statType)
        {
            if (CharGenView == null)
            {
                _logger.LogError("Unable to get leveling ability score vm due too missing CharGenView");
                return null;
            }

            if (CharGenView.SelectedDetailView is not CharGenAbilityScoresDetailedPCView abilityScoresDetailedPCView)
            {
                _logger.LogWarning("Unable to get leveling ability score vm because current phase is not skill phase");
                return null;
            }

            var abilityScoreView = abilityScoresDetailedPCView.m_StatAllocators.FirstOrDefault(x => x.ViewModel.StatType == statType);
            if (abilityScoreView == null)
            {
                _logger.LogWarning("Unable to find ability score leveling view for stat. StatType={StatType}", statType);
                return null;
            }

            return abilityScoreView;
        }

        private CharGenSpellsPhaseVM GetCharGenSpellsPhaseVM()
        {
            if (CharGenView == null)
            {
                _logger.LogError("Unable to get leveling spellphase vm due too missing CharGenView");
                return null;
            }

            if (CharGenView.SelectedDetailView is not CharGenSpellsPhaseDetailedPCView spellsPhaseDetailedPCView)
            {
                _logger.LogWarning("Unable to get leveling spellphase vm because current phase is not spell phase");
                return null;
            }

            return spellsPhaseDetailedPCView.ViewModel;
        }

        private CharGenClassPhaseVM GetLevelingPhaseViewModel()
        {
            var viewModel = (CharGenView?.SelectedDetailView as CharGenClassPhaseDetailedPCView)?.ViewModel;
            return viewModel;
        }

        private SpellSlot GetSpellSlot(Spellbook spellbook, NetworkSpellSlot slot)
        {
            if (spellbook.m_MemorizedSpells.Length < slot.SpellLevel)
            {
                return null;
            }

            var spellLevel = spellbook.m_MemorizedSpells[slot.SpellLevel];
            var spellSlot = spellLevel.FirstOrDefault(s => s.Index == slot.Index && s.Type == slot.Type);

            return spellSlot;
        }

        private void RefreshSpellbookUI()
        {
            SpellbookMemorizingVM?.UpdateSlots();
        }

        private void RefreshVendorScreen()
        {
            if (VendorViewVM == null)
            {
                _logger.LogWarning("Unable to refresh vendor screen due to missing VendorViewVM");
                return;
            }

            try
            {
                VendorViewVM.UpdateVendorSide();
                VendorViewVM.UpdatePlayerSide();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while refreshing vendor screen");
                throw;
            }
        }

        private void AddItemToVendorSellCollection(NetworkVendorItemTransfer transfer)
        {
            var possibleItems = Game.Instance.Player.Inventory.Where(i => IsSameItem(i, transfer.Item) &&
                (string.IsNullOrEmpty(transfer.Item.HoldingSlotOwnerId) || string.Equals(i.HoldingSlot?.Owner?.Unit.UniqueId, transfer.Item.HoldingSlotOwnerId, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.Count)
                .ToList();

            MatchSameNumberOfItems(possibleItems, transfer.Count,
                item =>
                {
                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateVendorItemTransfer(item.UniqueId);
                    Game.Instance.Vendor.AddForSell(item, item.Count);
                    _logger.LogInformation("Vendor item has been transferred. ItemId={ItemId}, Count={Count}, ActionTarget={ActionTarget}, ActionType={ActionType}", item.UniqueId, item.Count, transfer.ItemActionTarget, transfer.ItemAction);
                });
        }

        private (ItemEntity itemEntity, Func<ItemEntity, ItemEntity> vendorAction) GetDataForVendorTransferAction(NetworkItem networkItem, int count, VendorItemActionTarget target, VendorItemAction itemAction)
        {
            (ItemsCollection source, Func<ItemEntity, ItemEntity> vendorAction) = target switch
            {
                VendorItemActionTarget.Sell when itemAction == VendorItemAction.Remove => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(Game.Instance.Vendor.ItemsForSell, i => Game.Instance.Vendor.RemoveFromSell(i, count)),
                VendorItemActionTarget.Buy when itemAction == VendorItemAction.Add => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(Game.Instance.Vendor.StoreItems, i => Game.Instance.Vendor.AddForBuy(i, count)),
                VendorItemActionTarget.Buy when itemAction == VendorItemAction.Remove => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(Game.Instance.Vendor.ItemsForBuy, i => Game.Instance.Vendor.RemoveFromBuy(i, count)),
                _ => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(null, null),
            };

            if (source == null)
            {
                _logger.LogError("Unable to find correct vendor item source. Target={Target}, ItemAction={ItemAction}", target, itemAction);
                return (null, null);
            }

            var item = source.Items.FirstOrDefault(i => IsSameItem(i, networkItem));
            return (item, vendorAction);
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
                _logger.LogInformation("Same camping scroll scribing recipe is already selected, skipping updates. BlueprintId={BlueprintId}", scrollItemBlueprintId);
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
                _logger.LogError("Unable update camping scroll scribing recipe due to missing blueprint id. BlueprintId={BlueprintId}", scrollItemBlueprintId);
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
                _logger.LogInformation("Same camping alchemist potion recipe is already selected, skipping updates. BlueprintId={BlueprintId}", potionBlueprintRecipeId);
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
                _logger.LogError("Unable update camping alchemist potion recipe due to missing blueprint id. BlueprintId={BlueprintId}", potionBlueprintRecipeId);
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
            _logger.LogInformation("Using nearest lootbag as a map object. MapObjectId={MapObjectId}, Position={Position}", lootbag?.UniqueId, lootbag?.Position);
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
                    _logger.LogWarning("Transfer item id is mismatched, updating... ItemId={ItemId}, NetworkItemId={NetworkItemId}, ItemName={ItemName}, NetworkItemName={NetworkItemName}",
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

        private ActivatableAbility FindActivatableAbility(UnitEntityData caster, NetworkActivatableAbility activatableAbility)
        {
            var ability = caster.ActivatableAbilities.Enumerable.FirstOrDefault(a => string.Equals(a.UniqueId, activatableAbility.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.NameForAcronym, activatableAbility.Name, StringComparison.OrdinalIgnoreCase));

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
                _logger.LogError("Unable to 4find ability due to missing spellbook. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookId={SpellbookId}", unit.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                return null;
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var spellConversionSource = GetKnownSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name) ?? GetMemorizedSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name);
                if (spellConversionSource == null)
                {
                    _logger.LogError("Can't find spell conversion source for converted ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                var convertedSpell = GetConvertedAbility(spellConversionSource, networkAbility);
                if (convertedSpell == null)
                {
                    _logger.LogError("Can't find target ability in spell conversion list. UnitId={UnitId}, AbilityId={abilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                _logger.LogInformation("Converted spell has been found. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return convertedSpell;
            }

            var knownSpell = GetKnownSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (knownSpell != null)
            {
                _logger.LogInformation("Spell has been found in known spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return knownSpell;
            }

            var memorizedSpell = GetMemorizedSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (memorizedSpell != null)
            {
                _logger.LogInformation("Spell has been found in memorized spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
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
                    _logger.LogInformation("Unable to find ability for conversion. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, abilityUse.ConvertedFromId);
                    return null;
                }
                var convertedAbility = GetConvertedAbility(conversionAbility.Data, abilityUse);
                if (convertedAbility == null)
                {
                    _logger.LogInformation("Unable to find ability in conversion list. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, abilityUse.ConvertedFromId);
                }

                return convertedAbility;
            }

            var byAbilityId = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, abilityUse.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Data.NameForAcronym, abilityUse.Name, StringComparison.OrdinalIgnoreCase));

            if (byAbilityId != null)
            {
                _logger.LogInformation("Ability has been found by abilityId. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, abilityUse.Id);
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
            var selectedUnits = click.SelectedUnits.Select(GetUnitEntity).ToList();
            var selectedUnit = selectedUnits.FirstOrDefault();
            var worldPosition = new Vector3(click.WorldPosition.X, click.WorldPosition.Y, click.WorldPosition.Z);

            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Executing click handler. HandlerType={HandlerType}, WorldPosition={WorldPosition}, TargetUnitId={TargetUnitId}, SelectedUnit={SelectedUnit}, VectorPathCount={VectorPathCount}",
                               clickEventHandler.GetType().Name, click.WorldPosition, targetUnit?.UniqueId, selectedUnit?.UniqueId, click.VectorPath.Count);

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(selectedUnits);
                    Game.Instance.SelectionCharacter.SelectedUnit.Value = selectedUnit;

                    if (click.ActionsState != null)
                    {
                        //UpdateActionsState(click.ActionsState);
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
                        _logger.LogInformation("Configured unit path. VectorsCount={VectorsCount}", pathForCurrentUnit);
                    }

                    if (Game.Instance.TurnBasedCombatController.CurrentTurn != null && click.AttackMode != null)
                    {
                        Game.Instance.TurnBasedCombatController.CurrentTurn.m_AttackMode = click.AttackMode.Value;
                        _logger.LogInformation("AttackMode has been set. AttackMode={AttackMode}", click.AttackMode.Value);
                    }

                    clickEventHandler.OnClick(targetUnit?.View?.gameObject, worldPosition, click.Button, simulate: false, click.MuteEvents, IsTMBClick: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to execute click handler. HandlerType={HandlerType}", clickEventHandler?.GetType().Name);
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
            if (System.Enum.TryParse<T>(value, out var parsed))
            {
                return parsed;
            }

            return null;
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

        private UnitEntityData GetUnitEntity(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                return null;
            }

            return Game.Instance.State.Units.All.FirstOrDefault(u => string.Equals(u.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
