using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.Crusade.GlobalMagic;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Armies;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Kingdom.UI;
using Kingmaker.PubSubSystem;
using Kingmaker.RandomEncounters;
using Kingmaker.RandomEncounters.Settings;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.Kingdom;
using Kingmaker.UI.MVVM._PCView.Crusade.Armies;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._PCView.Crusade.Recruit;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Kingmaker.UI.Settlement;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class GlobalMapInteractionService : IGlobalMapInteractionService
    {
        private readonly ILogger<GlobalMapInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IUIAccessor _uiAccessor;
        private readonly IUISyncCountersService _uiSyncCountersService;

        public GlobalMapInteractionService(
            ILogger<GlobalMapInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IGameStateLookupService gameStateLookupService,
            IUIAccessor uiAccessor,
            IUISyncCountersService uiSyncCountersService)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _gameStateLookupService = gameStateLookupService;
            _uiAccessor = uiAccessor;
            _uiSyncCountersService = uiSyncCountersService;
        }

        public void OpenGroupChanger()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GroupChangerView;
                if (view?.ViewModel != null)
                {
                    _logger.LogWarning("GroupChanger is already opened");
                    return;
                }

                _uiAccessor.CloseAllWindows();

                GlobalMapView.Instance.StartChangedPartyOnGlobalMap();
                _logger.LogInformation("GroupChanger has been opened");
            });
        }

        public void StartTravel(NetworkGlobalMapTravel travel)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = _gameStateLookupService.GetGlobalMapPoint(travel.Destination);
                if (point == null)
                {
                    _logger.LogError("Unable to find global map point. PointId={PointId}, PointName={PointName}", travel.Destination.Id, travel.Destination.Name);
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                modalMessage.ViewModel?.OnDeclinePressed();
                var messageBoxView = _uiAccessor.GlobalMapEnterMessagePCView;
                messageBoxView.ViewModel?.Close();

                var traveler = Game.Instance.GlobalMapController.SelectedTraveler;
                UpdateTraveler(traveler, travel.Traveler);

                GlobalMapTravelData globalMapTravelData = travel.Type switch
                {
                    NetworkGlobalMapPathType.Direction => GlobalMapView.Instance.State.PathManager.CalculatePathByDirection(traveler, point.Blueprint),
                    NetworkGlobalMapPathType.Exact => GlobalMapView.Instance.State.PathManager.CalculateTravelerPathToLocation(traveler, point.Blueprint),
                    _ => null
                };

                if (globalMapTravelData == null)
                {
                    _logger.LogError("Failed to calculate travel data. PathType={PathType}, Destination={DestinationId}, DestinationName={DestinationName}", travel.Type, point.Blueprint.AssetGuid.ToString(), point.name);
                    return;
                }

                traveler.StartTravel(globalMapTravelData, travel.FromClick);
                _logger.LogInformation("Global map traveler has been started. Type={Type}, FromClick={FromClick}, Destination={DestinationId}, DestinationName={DestinationName}", travel.Type, travel.FromClick, point.Blueprint.AssetGuid.ToString(), point.name);
            });
        }

        public void DeclineCommonPopup()
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    _logger.LogWarning("Global map common popup is missing");
                    return;
                }

                // reset state
                modalMessage.m_AcceptButton.Interactable = true;
                modalMessage.m_DeclineButton.Interactable = true;

                modalMessage.m_DeclineButton.OnLeftClick.Invoke();
                _logger.LogInformation("Global map common popup has been closed");
            });
        }

        public bool IsAtLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var targetPoint = _gameStateLookupService.GetGlobalMapPoint(globalMapLocation);
            return targetPoint != null && GlobalMapView.Instance.State.Player.Location == targetPoint.Blueprint;
        }

        public void ContinueTravel(NetworkGlobalMapTraveler travaler)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateTraveler(travaler);
                Game.Instance.GlobalMapController?.StartTravels();
            });
        }

        public void StopTravel(NetworkGlobalMapTraveler travaler)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateTraveler(travaler);

                GlobalMapUI.Instance.OnStop();
            });
        }

        public void UpdateEnterMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var messageBoxView = _uiAccessor.GlobalMapEnterMessagePCView;
                if (messageBoxView?.ViewModel == null)
                {
                    return;
                }

                messageBoxView.m_AcceptButton.Interactable = !messageBoxView.ViewModel.IsCurrentLocation || isInteractable;
                messageBoxView.m_DeclineButton.Interactable = !messageBoxView.ViewModel.IsCurrentLocation || isInteractable;

                var buttonText = messageBoxView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                if (messageBoxView.ViewModel.IsCurrentLocation)
                {
                    _uiSyncCountersService.UpdateButtonTextCounter(messageBoxView.m_AcceptText, readyPlayersCount, totalPlayersCount);
                    _uiSyncCountersService.UpdateButtonTextCounter(messageBoxView.m_DeclineText, readyPlayersCount, totalPlayersCount);
                }
                else
                {
                    _uiSyncCountersService.RemoveButtonTextCounter(messageBoxView.m_AcceptText);
                    _uiSyncCountersService.RemoveButtonTextCounter(messageBoxView.m_DeclineText);
                }

                _logger.LogInformation("Global Map Message box buttons have been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateCommonPopupUI(NetworkGlobalMapCommonPopup globalMapCommonPopup, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    return;
                }

                modalMessage.m_AcceptButton.Interactable = isInteractable;
                modalMessage.m_DeclineButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AcceptText, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_DeclineText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Global Map Common Popup buttons have been updated. Type={Type}, IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", globalMapCommonPopup?.Type, isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView?.ViewModel == null)
                {
                    return;
                }

                var modalMessage = _uiAccessor.GlobalMapPCView.m_GlobalMapRandomEncounterPCView;
                modalMessage.m_AvoidButton.Interactable = isInteractable;
                modalMessage.m_ContinueButton.Interactable = isInteractable;
                modalMessage.m_EnterButton.Interactable = isInteractable;

                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AvoidLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_ContinueLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_EnterLabel, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Encounter Message has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateCrusadeArmyBattleResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TacticalCombatResultsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update crusade army battle results due to missing view");
                    return;
                }

                view.m_CloseButton.Interactable = isInteractable;
                view.m_StartManualCombatButton.Interactable = isInteractable;

                _uiSyncCountersService.UpdateButtonTextCounter(view.m_CloseButtonLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(view.m_StartManualCombatButtonLabel, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseCrusadeArmyBattleResults()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TacticalCombatResultsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army battle results due to missing view");
                    return;
                }

                view.ViewModel.Close();
                _logger.LogInformation("BattleResults window has been closed");
            });
        }

        public void StartCrusadeArmyBattleResultsManualCombat()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TacticalCombatResultsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to start crusade army battle results manual combat due to missing view");
                    return;
                }

                view.ViewModel.StartManualCombat();
                _logger.LogInformation("Manual combat has been started via BattleResults");
            });
        }

        public void CloseCombatResults()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.CombatResultPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close combat results UI due to missing view");
                    return;
                }

                view.ViewModel.Close();
                _logger.LogInformation("Combat results window has been closed");
            });
        }

        public void UpdateCombatResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.CombatResultPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update combat results UI due to missing view");
                    return;
                }

                view.m_CloseButton.Interactable = isInteractable;
                var closeButtonText = view.m_CloseButton.GetComponentInChildren<TextMeshProUGUI>();
                _uiSyncCountersService.UpdateButtonTextCounter(closeButtonText, readyPlayersCount, totalPlayersCount);
                _logger.LogInformation("Combat results UI state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var globalMapView = _uiAccessor.GlobalMapPCView;
                if (globalMapView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update UI due to missing GlobalMapPCView");
                    return;
                }

                UpdateSharedKingdomGlobalMapUI(isInteractable, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("GlobalMap UI state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateKingdomUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var kingdomView = _uiAccessor.KingdomPCView;
                if (kingdomView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update UI due to missing GlobalMapPCView/KingdomPCView");
                    return;
                }

                UpdateSharedKingdomGlobalMapUI(isInteractable, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Kingdom UI state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void AcceptCommonPopup(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel != null)
                {
                    // reset state
                    modalMessage.m_AcceptButton.Interactable = true;
                    modalMessage.m_DeclineButton.Interactable = true;

                    modalMessage.m_AcceptButton.OnLeftClick?.Invoke();

                    _logger.LogInformation("Global map common popup has been accepted");
                    return;
                }
            });
        }

        public void AcceptLocationMessageBox()
        {
            _mainThreadAccessor.Post(() =>
            {
                var messageBoxView = _uiAccessor.GlobalMapEnterMessagePCView;
                if (messageBoxView?.ViewModel == null)
                {
                    _logger.LogError("Unable to accept missing global map message box");
                    return;
                }

                messageBoxView.ViewModel.Accept();
                _logger.LogInformation("Global map message box has been accepted");
            });
        }

        public void CloseLocationMessageBox()
        {
            _mainThreadAccessor.Post(() =>
            {
                var messageBoxView = _uiAccessor.GlobalMapEnterMessagePCView;
                if (messageBoxView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close missing global map message box");
                    return;
                }

                messageBoxView.ViewModel.Close();
                _logger.LogInformation("Global map message box has been closed");
            });
        }

        public void EnterLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = _gameStateLookupService.GetGlobalMapPoint(globalMapLocation);
                if (point == null || GlobalMapView.Instance.State.Player.Location != point)
                {
                    _logger.LogError("Unable to enter desynced global map location. ExpectedLocationId={ExpectedLocationId}, ExpectedLocationName={ExpectedLocationName}, ActualLocationId={ActualLocationId}, ActualLocationName={ActualLocationName}", globalMapLocation.Id, globalMapLocation.Name, point?.Blueprint.AssetGuid.ToString(), point?.Blueprint.name);
                    return;
                }

                _uiAccessor.GlobalMapEnterMessagePCView?.ViewModel?.Close();

                GlobalMapView.Instance.EnterLocation();
                _logger.LogInformation("Global map location has been entered. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
            });
        }

        public void AvoidEncounter()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    _logger.LogWarning("Unable to avoid global map encounter when global map view is not available");
                    return;
                }

                _uiAccessor.GlobalMapPCView.m_GlobalMapRandomEncounterPCView.ViewModel.Avoid();
                _logger.LogInformation("Global map encounter has been avoided");
            });
        }

        public void AcceptEncounter()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    _logger.LogWarning("Unable to avoid global map encounter when global map view is not available");
                    return;
                }

                _uiAccessor.GlobalMapPCView.m_GlobalMapRandomEncounterPCView.ViewModel.Accept();
                _logger.LogInformation("Global map encounter has been accepted");
            });
        }

        public void RollEncounter(NetworkGlobalMapEncounter globalMapEncounter)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var encounterId = BlueprintGuid.Parse(globalMapEncounter.BlueprintId);
                    var encounter = ResourcesLibrary.TryGetBlueprint<BlueprintRandomEncounter>(encounterId);
                    if (encounter == null)
                    {
                        _logger.LogError("Unable to find global map encounter. BlueprintId={BlueprintId}", globalMapEncounter.BlueprintId);
                        return;
                    }

                    var position = globalMapEncounter.Position.ToUnityVector3();

                    var combatRandomEncounterData = new CombatRandomEncounterData(encounter, position)
                    {
                        IsTraderRE = globalMapEncounter.IsTrader,
                        AvoidanceCheckResult = (RandomEncounterAvoidanceCheckResult)Enum.Parse(typeof(RandomEncounterAvoidanceCheckResult), globalMapEncounter.AvoidanceResult, true)
                    };

                    if (globalMapEncounter.Seed.HasValue)
                    {
                        var component = encounter.AreaEntrance.Area.GetComponent<CombatRandomEncounterAreaSettings>();
                        combatRandomEncounterData.RandomCombat = CombatRandomEncountersGenerator.GenerateRandomEncounter(globalMapEncounter.Seed.Value, GlobalMapView.Instance.CurrentZone, component, null);
                    }

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

        public void SkipDay()
        {
            _mainThreadAccessor.Post(() =>
            {
                var toolbarView = _uiAccessor.GlobalMapToolbarPCView;
                if (toolbarView?.ViewModel == null)
                {
                    _logger.LogError("Unable to skip day due to missing global map toolbar view");
                    return;
                }

                toolbarView.ViewModel.SkipDay();
                _logger.LogInformation("Global map day has been skipped");
            });
        }

        public void SetSelectedArmy(NetworkGlobalMapArmy globalMapArmy)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (globalMapArmy == null)
                {
                    Game.Instance.GlobalMapController.SetSelectedArmy(null);
                    return;
                }

                var armyState = _gameStateLookupService.GetGlobalMapArmy(globalMapArmy.Id);
                if (armyState == null)
                {
                    _logger.LogError("Unable to find army. ArmyId={ArmyId}", globalMapArmy.Id);
                    return;
                }

                Game.Instance.GlobalMapController.SetSelectedArmy(armyState);
                _logger.LogInformation("Selected army has been updated. ArmyId={ArmyId}", armyState.Id);
            });
        }

        public void ChangeArmyMode(NetworkGlobalMapTravelerMode travelerMode)
        {
            _mainThreadAccessor.Post(() =>
            {
                var viewModel = _uiAccessor.GlobalMapToolbarPCView?.ViewModel;
                if (viewModel == null)
                {
                    _logger.LogWarning("Unable to change army mode due to missing view model. TravelerMode={TravelerMode}", travelerMode);
                    return;
                }

                viewModel.ArmyMode.Value = travelerMode == NetworkGlobalMapTravelerMode.Army;
                _logger.LogInformation("Army mode has been changed. IsArmyMode={IsArmyMode}, TravelerMode={TravelerMode}", viewModel.ArmyMode.Value, travelerMode);
            });
        }

        public void SetAutoCrusadeCombat(bool isEnabled)
        {
            _mainThreadAccessor.Post(() =>
            {
                var settingsView = _uiAccessor.GlobalMapToolbarPCView?.m_SettingsView;
                // settings are not opened -> update UI settings directly
                if (settingsView?.ViewModel == null)
                {
                    Game.Instance.Player.UISettings.AutoTacticalCombat = isEnabled;
                    _logger.LogInformation("Auto crusade combat setting has been updated directly. IsEnabled={IsEnabled}", isEnabled);
                    return;
                }

                // update via view to make it visible for current player
                settingsView.m_AutoTacticalCombat.ViewModel.IsOn.Value = isEnabled;
                _logger.LogInformation("Auto crusade combat setting has been updated via view. IsEnabled={IsEnabled}", isEnabled);
            });
        }

        public void RunSplitRequestForCrusadeArmySquad(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(sourceSquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to run split request for crusade army squad due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position);
                    return;
                }

                var (targetView, targetSquadVM) = GetArmyInfoSquadVM(targetSquadSlot);
                if (targetSquadVM == null)
                {
                    _logger.LogWarning("Unable to run split request for crusade army squad due to missing target squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", targetSquadSlot.ArmyId, targetSquadSlot.Position);
                    return;
                }

                var errros = sourceView.ViewModel.m_State.Data.MergeSquads(sourceSquadVM.SquadPosition, targetSquadVM.SquadPosition, count);
                sourceView.ViewModel.SquadErrorsTrigger.Execute(errros);
                _logger.LogInformation("Split request for crusade army squad completed. SourceArmyId={SourceArmyId}, SourceSquadPosition={SourceSquadPosition}, TargetArmyId={TargetArmyId}, TargetSquadPosition={TargetSquadPosition}, Count={Count}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position, count);
            });
        }

        public void SwitchCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(sourceSquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to switch crusade army squads due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position);
                    return;
                }

                var (targetView, targetSquadVM) = GetArmyInfoSquadVM(targetSquadSlot);
                if (targetSquadVM == null)
                {
                    _logger.LogWarning("Unable to switch crusade army squads due to missing target squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", targetSquadSlot.ArmyId, targetSquadSlot.Position);
                    return;
                }

                sourceView.ViewModel.SwitchSquads(sourceSquadVM, targetSquadVM);
                _logger.LogInformation("Crusade army squads have been switched. SourceArmyId={SourceArmyId}, SourceSquadPosition={SourceSquadPosition}, TargetArmyId={TargetArmyId}, TargetSquadPosition={TargetSquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position);
            });
        }

        public void MergeCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(sourceSquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to merge crusade army squads due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position);
                    return;
                }

                var (targetView, targetSquadVM) = GetArmyInfoSquadVM(targetSquadSlot);
                if (targetSquadVM == null)
                {
                    _logger.LogWarning("Unable to merge crusade army squads due to missing target squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", targetSquadSlot.ArmyId, targetSquadSlot.Position);
                    return;
                }

                sourceView.ViewModel.MergeSquads(sourceSquadVM, targetSquadVM, count);
                _logger.LogInformation("Crusade army squads have been merged. SourceArmyId={SourceArmyId}, SourceSquadPosition={SourceSquadPosition}, TargetArmyId={TargetArmyId}, TargetSquadPosition={TargetSquadPosition}, Count={Count}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position, count);
            });
        }

        public void SplitCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, int count)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(globalMapArmySquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to split crusade army squads due to missing squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                    return;
                }

                sourceSquadVM.Army.Split(sourceSquadVM.Squad, count);
                _logger.LogInformation("Crusade army squad has been split. ArmyId={ArmyId}, SquadPosition={SquadPosition}, Count={Count}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position, count);
            });
        }

        public void MergeInOneCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(globalMapArmySquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to merge in one crusade army squads due to missing squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                    return;
                }

                sourceSquadVM.Army.MergeInOne(sourceSquadVM.Squad);
                _logger.LogInformation("Crusade army squad has been merged in one. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
            });
        }

        public void DismissCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(globalMapArmySquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to dismiss crusade army squads due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                    return;
                }

                ArmyDismissManager.Instance.DismissSquad(sourceSquadVM.Army, sourceSquadVM.Squad);
                _logger.LogInformation("Crusade army squad has been dismissed. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
            });
        }

        public void UpdateCrusadeArmyInfoUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update crusade army info due to missing view");
                    return;
                }

                view.m_NextMergeArmy.Interactable = view.ViewModel.HaveNextArmyMerge.Value && isInteractable;
                view.m_PrevMergeArmy.Interactable = view.ViewModel.HavePrevArmyMerge.Value && isInteractable;
                view.m_CreateArmyButton.Interactable = isInteractable;
                view.m_RecruitArmyButton.Interactable = view.ViewModel.CanRecruit.Value && isInteractable;
                view.m_MoveSquadsToMainButton.Interactable = isInteractable;
                view.m_MoveSquadsToSecondButton.Interactable = isInteractable;

                UpdateCrusadeArmySetLeaderUI(view.m_SetLeaderView, isInteractable, readyPlayersCount, totalPlayersCount);

                var mainCartView = view.m_MainArmyCartView;
                UpdateArmyCartView(mainCartView, isInteractable, readyPlayersCount, totalPlayersCount);

                var mergeCartView = view.m_MergeArmyCartView;
                UpdateArmyCartView(mergeCartView, isInteractable, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Crusade army info ui has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseCrusadeArmyInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army info due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army info close handler has been executed");
            });
        }

        public void CloseCrusadeArmyMainInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView?.m_MainArmyCartView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army main info due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army main info close handler has been executed");
            });
        }

        public void CloseCrusadeArmySetLeaderInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView?.m_SetLeaderView;
                if (view?.ViewModel == null)
                {
                    view = _uiAccessor.RecruitPCView?.m_LeaderSetView;
                    if (view?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to close crusade army info set leader due to missing view");
                        return;
                    }
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army set leader close handler has been executed");
            });
        }

        public void ClearLeaderOnCrusdeArmyInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView?.m_SetLeaderView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to clear leader on crusade army info due to missing view");
                    return;
                }

                view.ViewModel.OnClearLeader();
                _logger.LogInformation("ClearLeader has been executed on crusade army info set leader");
            });
        }

        public void ClickRecruitmentOnSetLeaderScreen()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView?.m_SetLeaderView;
                if (view?.ViewModel == null)
                {
                    view = _uiAccessor.RecruitPCView?.m_LeaderSetView;
                    if (view?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to open leader recruitment due to missing view");
                        return;
                    }
                }

                view.ViewModel.OnBuyLeader();
                _logger.LogInformation("OnBuyLeader has been executed on crusade army info set leader");
            });
        }

        public void CloseBuyLeaderScreen()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyCartBuyLeaderPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close buy leader screen due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Buy leader screen has been closed");
            });
        }

        public void UpdateSharedCrusadeManagementUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.ArmyInfoPCView?.ViewModel != null)
                {
                    UpdateCrusadeArmyInfoUI(isInteractable, readyPlayersCount, totalPlayersCount);
                }
                else if (_uiAccessor.RecruitPCView?.ViewModel != null)
                {
                    UpdateRecruitmentUI(isInteractable, readyPlayersCount, totalPlayersCount);
                }
            });
        }

        public void SelectNextRecruitmentArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RecruitPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select next recruitment army due to missing view");
                    return;
                }

                view.ViewModel.NextArmy();
                _logger.LogInformation("Next recruitment army has been selected");
            });
        }

        public void SelectPrevRecruitmentArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RecruitPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select prev recruitment army due to missing view");
                    return;
                }

                view.ViewModel.PrevArmy();
                _logger.LogInformation("Prev recruitment army has been selected");
            });
        }

        public void RerollRecruitmentMercenaries()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RecruitPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to reroll recruitment mercenaries due to missing view");
                    return;
                }

                view.OnMercReroll();
                _logger.LogInformation("Recruitment mercenaries have been rerolled");
            });
        }

        /// <summary>
        /// RecruitBuyResourcesVM.Buy
        /// </summary>
        /// <param name="globalMapResourceOrder"></param>
        public void BuyResources(NetworkGlobalMapResourceOrder globalMapResourceOrder)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!Game.Instance.Player.SpendMoney(globalMapResourceOrder.FinalCost))
                {
                    _logger.LogError("Failed to spend money on resoures.");
                    return;
                }

                var changes = new KingdomStats.Changes();
                KingdomResourcesAmount delta = KingdomResourcesAmount.FromFinances(globalMapResourceOrder.FinanceCount) + KingdomResourcesAmount.FromMaterials(globalMapResourceOrder.MaterialCount);
                changes.ResourcesOneTime += delta;
                changes.Apply(null, true);
                UISoundController.Instance.Play(UISoundType.ArmyManagementBuyResourcesPlay);
                EventBus.RaiseEvent<IKingdomResourcesHandler>(x => x.OnResourcesChanged(delta));

                if (_uiAccessor.RecruitPCView?.ViewModel != null)
                {
                    DisableRecruitUI(_uiAccessor.RecruitPCView);
                }
            });
        }

        public void BuyUnits(NetworkGlobalMapUnitRecruitmentOrder globalMapUnitRecruitmentOrder)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RecruitPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogError("Unable to buy units due to missing view");
                    return;
                }

                switch (globalMapUnitRecruitmentOrder.Type)
                {
                    case NetworkGlobalMapUnitRecruitmentType.Unit:
                        var unit = view.ViewModel.m_Shop.FirstOrDefault(s => string.Equals(s.Recruit.Unit.AssetGuid.ToString(), globalMapUnitRecruitmentOrder.BlueprintId, StringComparison.OrdinalIgnoreCase) && s.Recruit.Count >= globalMapUnitRecruitmentOrder.Count);
                        if (unit == null)
                        {
                            _logger.LogError("Unable to find valid unit to buy. UnitId={UnitId}, Count={Count}", globalMapUnitRecruitmentOrder.BlueprintId, globalMapUnitRecruitmentOrder.Count);
                            return;
                        }
                        view.ViewModel.BuyRecruit(unit.Recruit.Unit, globalMapUnitRecruitmentOrder.Count);
                        break;
                    case NetworkGlobalMapUnitRecruitmentType.Mercenary:
                        var mercenary = view.ViewModel.m_MercShop.FirstOrDefault(s => string.Equals(s.Recruit.Unit.AssetGuid.ToString(), globalMapUnitRecruitmentOrder.BlueprintId, StringComparison.OrdinalIgnoreCase) && s.Recruit.Count == globalMapUnitRecruitmentOrder.Count);
                        if (mercenary == null)
                        {
                            _logger.LogError("Unable to find valid mercenary to buy. UnitId={UnitId}, Count={Count}", globalMapUnitRecruitmentOrder.BlueprintId, globalMapUnitRecruitmentOrder.Count);
                            return;
                        }
                        var army = _gameStateLookupService.GetGlobalMapArmy(globalMapUnitRecruitmentOrder.ArmyId);
                        if (army == null)
                        {
                            _logger.LogError("Unable to find valid army to buy mercenary for. ArmyId={ArmyId}", globalMapUnitRecruitmentOrder.ArmyId);
                            return;
                        }
                        bool isOk = KingdomState.Instance.MercenariesManager.Recruit(army.Data, mercenary.MercenarySlot);
                        view.ViewModel.m_AvailableResources.Value = KingdomState.Instance.Resources;
                        if (!isOk)
                        {
                            _logger.LogError("Failed to buy mercenary. ArmyId={ArmyId}, UnitId={UnitId}, Count={Count}", globalMapUnitRecruitmentOrder.ArmyId, globalMapUnitRecruitmentOrder.BlueprintId, globalMapUnitRecruitmentOrder.Count);
                            return;
                        }

                        UISoundController.Instance.Play(UISoundType.ArmyManagementHireTroopsPlay);
                        foreach (var mercVM in view.ViewModel.m_MercShop)
                        {
                            mercVM.UpdateMercenarySlot();
                            mercVM.CanBuy.Value = false;
                        }
                        break;
                    default:
                        _logger.LogError("Unsupported unit recruitment type. Type={Type}", globalMapUnitRecruitmentOrder.Type);
                        return;
                }

                DisableRecruitUI(view);
                _logger.LogInformation("Mercenaries have been bought. ArmyId={ArmyId}, UnitId={UnitId}, Count={Count}", globalMapUnitRecruitmentOrder.ArmyId, globalMapUnitRecruitmentOrder.BlueprintId, globalMapUnitRecruitmentOrder.Count);
            });
        }

        public void OpenRecruitments()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.RecruitPCView?.ViewModel != null)
                {
                    _logger.LogWarning("Recruitments is already opened");
                    return;
                }

                _uiAccessor.GlobalMapMenuPCView.ViewModel.OnRecruitClick();
                _logger.LogInformation("Recruitment UI has been opened");
            });
        }

        public void CloseRecruitments()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RecruitPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Recruitments is already closed");
                    return;
                }

                view.ViewModel.Close();
                _logger.LogInformation("Recruitment UI has been closed");
            });
        }

        public void UpdateRecruitmentUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RecruitPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update recruitment due to missing view");
                    return;
                }

                view.m_Close.Interactable = isInteractable;
                view.m_CreateArmyButton.Interactable = view.ViewModel.CanCreateArmy.Value && isInteractable;
                view.m_BuyResourceButton.Interactable = isInteractable;
                view.m_MercRerollButton.Interactable = view.CanMercReroll.Value && isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(view.m_BuyResourceLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(view.m_MercRerollText, readyPlayersCount, totalPlayersCount);

                view.m_NextArmy.Interactable = view.ViewModel.HaveNextArmy.Value && isInteractable;
                view.m_PrevArmy.Interactable = view.ViewModel.HavePrevArmy.Value && isInteractable;

                UpdateRecruitmentUnits(view.m_ShopUnits, isInteractable);
                UpdateRecruitmentUnits(view.m_MercUnits, isInteractable);

                UpdateCrusadeArmySetLeaderUI(view.m_LeaderSetView, isInteractable, readyPlayersCount, totalPlayersCount);
                UpdateArmyCartView(view.m_ArmyView, isInteractable, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Recruitment UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateBuyLeaderUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyCartBuyLeaderPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update buy leader screen due to missing view");
                    return;
                }

                view.m_CloseButton.Interactable = isInteractable;

                foreach (var leader in view.m_Leaders)
                {
                    UpdateLeaderInfoUIState((ArmyLeaderInfoPCView)leader, isInteractable, readyPlayersCount, totalPlayersCount);
                }

                _logger.LogInformation("Crusade army info ui has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseCrusadeArmyMergeInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView?.m_MergeArmyCartView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army merge info due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army info close handler has been executed");
            });
        }

        public void SetCrusadeArmyInfoCartName(NetworkGlobalMapArmy globalMapArmy)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = GetArmyInfoCart(globalMapArmy);
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to change crusade army name due to missing view. ArmyId={ArmyId}", globalMapArmy.Id);
                    return;
                }

                view.ViewModel.SetArmyName(globalMapArmy.Name);
                _logger.LogInformation("Crusade army name has been set. ArmyId={ArmyId}, Name={Name}", view.ViewModel.State.Id, globalMapArmy.Name);
            });
        }

        public void OpenCrusadeArmyInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoHUDPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to open crusade army info due to missing view");
                    return;
                }

                if (_uiAccessor.ArmyInfoPCView?.ViewModel != null)
                {
                    _logger.LogWarning("Army info is already opened");
                    return;
                }

                view.ViewModel.OnInfoRequest();
                _logger.LogInformation("Crusade army info has been opened");
            });
        }

        public void MoveCrusadeArmySquadsToMainArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to move crusade army squads to main army due to missing view");
                    return;
                }

                view.ViewModel.MoveSquadsToMainArmy();
                _logger.LogInformation("Crusade army squads have been moved to main army");
            });
        }

        public void MoveCrusadeArmySquadsToSecondArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to move crusade army squads to second army due to missing view");
                    return;
                }

                view.ViewModel.MoveSquadsToSecondArmy();
                _logger.LogInformation("Crusade army squads have been moved to second army");
            });
        }

        public void SelectPrevCrusadeArmyInfoMergeArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select prev crusade army merge due to missing view");
                    return;
                }

                view.ViewModel.PrevMergeArmy();
                _logger.LogInformation("Prev army for crusade army merge has been selected");
            });
        }

        public void SelectNextCrusadeArmyInfoMergeArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select next crusade army merge due to missing view");
                    return;
                }

                view.ViewModel.NextMergeArmy();
                _logger.LogInformation("Next army for crusade army merge has been selected");
            });
        }

        public void RunLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType armyLeaderActionType)
        {
            _mainThreadAccessor.Post(() =>
            {
                var viewModel = GetArmyLeaderView(globalMapArmyLeader, armyLeaderActionType);
                if (viewModel == null)
                {
                    _logger.LogWarning("Unable to run leader action due to missing viewModel. LeaderId={LeaderId}, ActionType={ActionType}", globalMapArmyLeader?.Id, armyLeaderActionType);
                    return;
                }

                switch (armyLeaderActionType)
                {
                    case NetworkGlobalMapArmyLeaderActionType.Main:
                        viewModel.OnClick();
                        break;
                    case NetworkGlobalMapArmyLeaderActionType.LevelUp:
                        viewModel.OnLevelUp();
                        break;
                    case NetworkGlobalMapArmyLeaderActionType.MainLookAtPool:
                    case NetworkGlobalMapArmyLeaderActionType.MergeLookAtPool:
                        viewModel.OnLookAtLeaderPool();
                        break;
                    default:
                        _logger.LogWarning("Unknown leader action type. ActionType={ActionType}", armyLeaderActionType);
                        return;
                }
                _logger.LogInformation("Army leader action has been executed. LeaderId={LeaderId}, ActionType={ActionType}", globalMapArmyLeader?.Id, armyLeaderActionType);
            });
        }

        public void OpenCrusadeArmiesMergeScreen()
        {
            _mainThreadAccessor.Post(() =>
            {

                var overtipVM = _uiAccessor.GlobalMapPCView?.ViewModel?.GlobalMapArmyOvertipsVM ?? _uiAccessor.KingdomPCView?.ViewModel?.GlobalMapArmyOvertipsVM;
                if (overtipVM == null)
                {
                    _logger.LogWarning("Unable to open merge screen due to missing overtip viewmodel");
                    return;
                }

                overtipVM.MergeArmies();
                _logger.LogInformation("Merge armies screen has been opened");
            });
        }

        public void DismissCrusadeArmy(NetworkGlobalMapArmy globalMapArmy)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = GetArmyInfoCart(globalMapArmy);
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to dismiss crusade army due to missing view. ArmyId={ArmyId}", globalMapArmy.Id);
                    return;
                }

                view.ViewModel.DismissAll();
                _logger.LogInformation("Crusade army dismiss request has been opened. ArmyId={ArmyId}", globalMapArmy.Id);
            });
        }

        public void CreateCrusadeArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    var recruitView = _uiAccessor.RecruitPCView;
                    if (recruitView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to create army at crusade info due to missing view");
                        return;
                    }

                    recruitView.CreateArmy();
                    return;
                }

                // view call to play sound
                view.CreateArmy();
                _logger.LogInformation("Crusade army has been created");
            });
        }

        public void UseSpell(NetworkGlobalMapMagicSpell globalMapMagicSpell)
        {
            _mainThreadAccessor.Post(() =>
            {
                var spellState = Game.Instance.Player.GlobalMapSpellsManager.SpellBook.FirstOrDefault(x => string.Equals(x.Blueprint.AssetGuid.ToString(), globalMapMagicSpell.Id, StringComparison.OrdinalIgnoreCase));
                if (spellState == null)
                {
                    _logger.LogError("Unable to use global magic spell due to missing spell. SpellId={SpellId}", globalMapMagicSpell.Id);
                    return;
                }

                var pointState = _gameStateLookupService.GetGlobalMapPoint(globalMapMagicSpell.Location)?.State;
                var spellWasUsed = UseSpell(spellState.Blueprint, globalMapMagicSpell.TargetArmies, pointState);
                if (spellWasUsed)
                {
                    Game.Instance.Player.GlobalMapSpellsManager.SpellWasUsed(spellState.Blueprint, false);
                }

                _logger.LogInformation("Global map magic spell usage has been processed. SpellId={SpellId}, SpellName={SpellName}, SpellWasActuallyUsed={SpellWasActuallyUsed}, TargetArmies={TargetArmies}, PointId={PointId}", globalMapMagicSpell.Id, globalMapMagicSpell.Name, spellWasUsed, globalMapMagicSpell.TargetArmies, globalMapMagicSpell.Location?.Id);
            });
        }

        private bool UseSpell(BlueprintGlobalMagicSpell spell, List<string> armies, GlobalMapPointState pointState)
        {
            using var context = ContextData<BlueprintGlobalMagicSpell.GlobalMagicData>.Request();
            if (armies == null || armies.Count == 0)
            {
                UseSpell(context, spell, armyState: null, pointState);
                return true;
            }

            var spellWasUsed = false;
            foreach (var armyId in armies)
            {
                var army = _gameStateLookupService.GetGlobalMapArmy(armyId);
                if (army == null)
                {
                    _logger.LogError("Unable to use global magic spell due to missing target army. ArmyId={ArmyId}", armyId);
                    continue;
                }

                UseSpell(context, spell, army, pointState);
                spellWasUsed = true;
            }

            return spellWasUsed;
        }

        public void UpdateLeaderLevelingUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.LeaderLevelUpPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update leader leveling ui due to missing view");
                    return;
                }

                view.m_CloseButton.Interactable = isInteractable;
                view.m_ConfirmButton.Interactable = view.CanConfirm.Value && isInteractable;
                foreach (var skill in view.m_Skills)
                {
                    skill.GetComponent<ObservablePointerClickTrigger>().enabled = isInteractable;
                }

                _uiSyncCountersService.UpdateButtonTextCounter(view.m_ConfirmButtonText, readyPlayersCount, totalPlayersCount);
                _logger.LogInformation("Leader leveling ui has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseLeaderLeveling()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.LeaderLevelUpPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close leader leveling due to missing view");
                    return;
                }

                view.ViewModel.Close();
                _logger.LogInformation("Army Leader leveling has been closed");
            });
        }

        public void ConfirmLeaderLeveling()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.LeaderLevelUpPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to confirm leader leveling due to missing view");
                    return;
                }

                view.ViewModel.Confirm();
                _logger.LogInformation("Army Leader leveling has been confirmed");
            });
        }

        public void SelectLeaderLevelingSkill(string id)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.LeaderLevelUpPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select leader leveling skll to missing view");
                    return;
                }

                var skill = view.ViewModel.SkillsToChoose.FirstOrDefault(s => string.Equals(s.Skill.AssetGuid.ToString(), id, StringComparison.OrdinalIgnoreCase));
                if (skill == null)
                {
                    _logger.LogError("Unable to select leader leveling skll to missing skill. Id={Id}", id);
                    return;
                }

                view.ViewModel.SelectedSkill.Value = skill.Skill;
                _logger.LogInformation("Army Leader leveling skill has been selected. Id={Id}, Name={Name}", view.ViewModel.SelectedSkill.Value.AssetGuid.ToString(), view.ViewModel.SelectedSkill.Value.name);
            });
        }

        public void StartCrusadeArmyLeaderLeveling(NetworkGlobalMapArmy globalMapArmy)
        {
            _mainThreadAccessor.Post(() =>
            {
                var army = _gameStateLookupService.GetGlobalMapArmy(globalMapArmy.Id);
                if (army?.Data?.Leader == null)
                {
                    _logger.LogError("Unable to find army leader to start leader. ArmyId={ArmyId}", globalMapArmy.Id);
                    return;
                }

                EventBus.RaiseEvent<ILeaderLevelupHandler>(x => x.ShowLeaderLevelUp(army.Data.Leader));
                _logger.LogInformation("Army leader leveling has been started. ArmyId={ArmyId}", globalMapArmy.Id);
            });
        }

        public Task<bool> ShowCommonPopupAsync(NetworkGlobalMapCommonPopup popup)
        {
            var tcs = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    switch (popup.Type)
                    {
                        case NetworkGlobalMapCommonPopupType.Fatigue:
                            UIUtility.ShowMessageBox(UIStrings.Instance.GlobalMap.PartyIsFatigue, MessageModalBase.ModalType.Dialog, _uiAccessor.GlobalMapPCView.ViewModel.OnFatigueClose);
                            tcs.SetResult(true);
                            return;
                        case NetworkGlobalMapCommonPopupType.Exhaust:
                            UIUtility.ShowMessageBox(UIStrings.Instance.GlobalMap.PartyIsExhausted, MessageModalBase.ModalType.Dialog, _uiAccessor.GlobalMapPCView.ViewModel.OnFatigueClose);
                            tcs.SetResult(true);
                            return;
                    }

                    tcs.SetResult(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while showing popup");
                    tcs.SetResult(false);
                }
            });

            return tcs.Task;
        }

        public void EnterKingdom(NetworkKingdomEntryPoint kingdomEntryPoint)
        {
            _mainThreadAccessor.Post(() =>
            {
                BlueprintAreaEnterPoint point = null;
                if (!string.IsNullOrEmpty(kingdomEntryPoint.Id))
                {
                    point = ResourcesLibrary.TryGetBlueprint<BlueprintAreaEnterPoint>(kingdomEntryPoint.Id);
                    if (point == null)
                    {
                        _logger.LogError("Unable to find kingdom enter point. Id={Id}", kingdomEntryPoint.Id);
                        return;
                    }
                }

                SettlementState settlementState = null;
                if (kingdomEntryPoint.Settlement != null)
                {
                    settlementState = _gameStateLookupService.GetKingdomSettlement(kingdomEntryPoint.Settlement);
                    if (settlementState == null)
                    {
                        _logger.LogError("Unable to find kingdom settlement to focus. Id={Id}, Name={Name}", kingdomEntryPoint.Settlement.Id, kingdomEntryPoint.Settlement.Name);
                        return;
                    }
                }

                Game.Instance.KingdomController.EnterKingdomArea(point, settlementState);
                _logger.LogInformation("Kingdom has been entered. EnterPointId={EnterPointId}, SettlementId={SettlementId}, SettlementName={SettlementName}", point?.AssetGuid.ToString(), settlementState?.UniqueId, settlementState?.Name);
            });
        }

        public void ExitKingdom()
        {
            _mainThreadAccessor.Post(() =>
            {
                UIUtility.LeaveKingdom();
                _logger.LogInformation("Kingdom has been exited");
            });
        }

        public void ChangeKingdomNavigation(KingdomNavigationType kingdomNavigationType)
        {
            _mainThreadAccessor.Post(() =>
            {
                KingdomState.Instance.NavigationType = kingdomNavigationType;
                _logger.LogInformation("Kingdom navigation type has been changed. NavigationType={NavigationType}", kingdomNavigationType);
            });
        }

        public void SelectKingdomEvent(NetworkKingdomEvent kingdomEvent)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (kingdomEvent == null)
                {
                    EventBus.RaiseEvent<IKingdomUICloseEventWindow>(x => x.OnClose());
                    _logger.LogInformation("Kingdom event window has been closed.");
                    return;
                }

                var (cart, eventView) = FindEvent(kingdomEvent);
                if (eventView == null)
                {
                    _logger.LogError("Unable to find kingdom event. Id={Id}", kingdomEvent.Id);
                    return;
                }

                EventBus.RaiseEvent<IEventSceneHandler>(x => x.OnEventSelected(eventView, cart));
                _logger.LogInformation("Kingdom event has been selected. Id={Id}, Name={Name}", eventView?.Event.EventBlueprint.AssetGuid.ToString(), eventView?.Event.FullName);
            });
        }

        public void SelectKingdomEventSolution(NetworkKingdomEventSolution kingdomEventSolution)
        {
            _mainThreadAccessor.Post(() =>
            {
                var eventWindowFooter = UnityEngine.Object.FindObjectOfType<KingdomUIEventWindowFooter>();
                if (eventWindowFooter == null)
                {
                    _logger.LogError("Unable to select event solution due to missing KingdomUIEventWindowFooter");
                    return;
                }

                if (kingdomEventSolution == null)
                {
                    foreach (var solution in eventWindowFooter.m_Solutions.m_Solutions)
                    {
                        solution.Toggle.isOn = false;
                    }
                    _logger.LogInformation("Kingdom event solution has been reset");
                    return;
                }

                if (kingdomEventSolution.Index >= eventWindowFooter.m_Solutions.m_Solutions.Count)
                {
                    _logger.LogError("Kingdom event solution index is out of range. Index={Index}, SolutionsCount={SolutionsCount}", kingdomEventSolution.Index, eventWindowFooter.m_Solutions.m_Solutions.Count);
                    return;
                }

                var selectedSolution = eventWindowFooter.m_Solutions.m_Solutions[kingdomEventSolution.Index];
                selectedSolution.SetOn();
                _logger.LogInformation("Kingdom event solution has been selected. Index={Index}, Name={Name}", kingdomEventSolution.Index, selectedSolution.EventSolution.m_SolutionText);
            });
        }

        public void StartKingdomEvent()
        {
            _mainThreadAccessor.Post(() =>
            {
                var eventWindowFooter = UnityEngine.Object.FindObjectOfType<KingdomUIEventWindowFooter>();
                if (eventWindowFooter == null)
                {
                    _logger.LogError("Unable to start event due to missing KingdomUIEventWindowFooter");
                    return;
                }

                eventWindowFooter.OnStart();
                _logger.LogInformation("Kingdom event has been started. SolutionName={SolutionName}", eventWindowFooter.CurrentEventSolution?.SolutionText);
            });
        }

        public void CancelKingdomEvent()
        {
            _mainThreadAccessor.Post(() =>
            {
                var eventWindowFooter = UnityEngine.Object.FindObjectOfType<KingdomUIEventWindowFooter>();
                if (eventWindowFooter == null)
                {
                    _logger.LogError("Unable to cancel event due to missing KingdomUIEventWindowFooter");
                    return;
                }

                _logger.LogInformation("Kingdom event has been cancelled. SolutionName={SolutionName}", eventWindowFooter.CurrentEventSolution?.SolutionText);
                eventWindowFooter.OnCancelEvent();
            });
        }

        public void DropKingdomEvent(NetworkKingdomEvent kingdomEvent)
        {
            _mainThreadAccessor.Post(() =>
            {
                var eventWindowFooter = UnityEngine.Object.FindObjectOfType<KingdomUIEventWindowFooter>();
                if (eventWindowFooter == null)
                {
                    var (_, eventView) = FindEvent(kingdomEvent);
                    eventView.DropEvent();
                    EventBus.RaiseEvent<IKingdomTaskChangedHandler>(x => x.OnTaskChanged());
                    _logger.LogInformation("Event has been dropped via fallback. EventId={EventId}", kingdomEvent.Id);
                    return;
                }

                eventWindowFooter.m_KingdomEventView.DropEvent();
                _logger.LogInformation("Kingdom event has been dropped. EventId={EventId}", eventWindowFooter.m_KingdomEventView.Event.EventBlueprint.AssetGuid.ToString());
            });
        }

        public void EnterSettlement(NetworkKingdomSettlement kingdomSettlement, bool requiresUnloadEvent, bool exitSettlementToGlobalMap)
        {
            _mainThreadAccessor.Post(() =>
            {
                var settlement = _gameStateLookupService.GetKingdomSettlement(kingdomSettlement);
                if (settlement == null)
                {
                    _logger.LogError("Unable to enter missing settlement. Id={Id}", kingdomSettlement.Id);
                    return;
                }

                KingdomState.Instance.ExitSettlementToGlobalMap = exitSettlementToGlobalMap;
                KingdomState.Instance.CurrentSettlement = settlement;
                if (requiresUnloadEvent)
                {
                    EventBus.RaiseEvent<IKingdomSceneHandler>(x => x.OnKingdomSceneWillUnload());
                }

                Game.Instance.LoadArea(settlement.SettlementBuildArea, AutoSaveMode.None, null);
                _logger.LogInformation("Kingdom settlement entering process has been initiated. SettlementId={SettlementId}, SettlementName={SettlementName}, RequiresUnloadEvent={RequiresUnloadEvent}, ExitSettlementToGlobalMap={ExitSettlementToGlobalMap}", settlement.UniqueId, settlement.Name, requiresUnloadEvent, exitSettlementToGlobalMap);
            });
        }

        public void LeaveSettlement()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.CityBuilderPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update settlement ui due to missing CityBuilderPCView");
                    return;
                }

                view.ViewModel.LeaveCityBuilder();
                _logger.LogError("Kingdom settlement has been left.");
            });
        }

        public void UpdateSettlementUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.CityBuilderPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update settlement ui due to missing CityBuilderPCView");
                    return;
                }

                var toolbar = UnityEngine.Object.FindObjectOfType<SettlementToolbar>();
                if (toolbar != null)
                {
                    var buttonText = toolbar.m_BuyResourceButton.GetComponentInChildren<TextMeshProUGUI>();
                    _uiSyncCountersService.UpdateButtonTextCounter(buttonText, readyPlayersCount, totalPlayersCount);
                    toolbar.m_BuyResourceButton.Interactable = isInteractable;
                }

                var statsController = UnityEngine.Object.FindObjectOfType<KingdomStatsController>();
                if (statsController != null)
                {
                    statsController.m_Content.m_BuyResourceButton.Interactable = isInteractable;
                }

                _logger.LogInformation("Settlement UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        private (KingdomEventHandCartController, KingdomEventUIView) FindEvent(NetworkKingdomEvent networkKingdomEvent)
        {
            if (networkKingdomEvent == null)
            {
                return (null, null);
            }

            var eventBarController = UnityEngine.Object.FindObjectOfType<KingdomEventBarController>();
            if (eventBarController == null)
            {
                _logger.LogError("Unable to find KingdomEventBarController");
                return (null, null);
            }

            var eventInfo = FindEvent(eventBarController.Available, networkKingdomEvent)
                ?? FindEvent(eventBarController.InProgress, networkKingdomEvent)
                ?? FindEvent(eventBarController.Complete, networkKingdomEvent);

            if (eventInfo == null)
            {
                return (null, null);
            }

            return (eventInfo?.Cart, eventInfo?.View);
        }

        private (KingdomEventHandCartController Cart, KingdomEventUIView View)? FindEvent(KingdomEventHand kingdomEventHand, NetworkKingdomEvent networkKingdomEvent)
        {
            var task = kingdomEventHand.m_Tasks.FirstOrDefault(t => string.Equals(t.Event.EventBlueprint.AssetGuid.ToString(), networkKingdomEvent.Id, StringComparison.OrdinalIgnoreCase));
            if (task == null)
            {
                return null;
            }

            return (kingdomEventHand.Cart, task);
        }

        private void UpdateGlobalMapToolbar(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            var toolbar = _uiAccessor.GlobalMapToolbarPCView;
            if (toolbar?.ViewModel == null)
            {
                return;
            }

            toolbar.m_ArmyModeCharacterButton.Interactable = isInteractable;
            toolbar.m_ArmyModeCrusadeButton.Interactable = isInteractable;
            toolbar.m_SkipDay.Interactable = isInteractable;
            if (toolbar.m_AddArmyCrusadeButton != null)
            {
                toolbar.m_AddArmyCrusadeButton.Interactable = isInteractable;
            }
            if (toolbar.m_AddArmyCrusadePlusButton != null)
            {
                toolbar.m_AddArmyCrusadePlusButton.Interactable = isInteractable;
            }
            _uiSyncCountersService.UpdateButtonTextCounter(toolbar.m_SkipDayLabel, readyPlayersCount, totalPlayersCount);
        }

        private void UpdateGlobalMapKingdomInfo(bool isInteractable)
        {
            var kingdomInfoPCView = _uiAccessor.KingdomInfoPCView;
            if (kingdomInfoPCView?.ViewModel == null)
            {
                return;
            }

            kingdomInfoPCView.m_BuyResourcesButton.Interactable = isInteractable;
        }

        private void UpdateGlobalMapArmies(bool isInteractable)
        {
            var armiesPCView = _uiAccessor.GlobalMapCrusadeArmiesPCView;
            if (armiesPCView?.ViewModel == null)
            {
                return;
            }

            var armies = armiesPCView.m_ArmiesContainer?.gameObject.GetComponentsInChildren<GlobalMapCrusadeArmyPCView>() ?? [];
            foreach (var army in armies)
            {
                army.m_SelectButton.Interactable = isInteractable;
                army.m_SettingsButton.Interactable = isInteractable;
            }
        }

        private void UpdateSharedKingdomGlobalMapUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            UpdateGlobalMapMovementButtons(isInteractable);

            UpdateGlobalMapToolbar(isInteractable, readyPlayersCount, totalPlayersCount);
            UpdateGlobalMapArmies(isInteractable);
            UpdateGlobalMapArmyHud(isInteractable);

            UpdateGlobalMapKingdomInfo(isInteractable);
            UpdateGlobalMapMenuView(isInteractable);
            UpdateGlobalMapArmyMarkers(isInteractable);
        }

        private void UpdateGlobalMapArmyHud(bool isInteractable)
        {
            var armyInfoHUDPCView = _uiAccessor.ArmyInfoHUDPCView;
            if (armyInfoHUDPCView?.ViewModel == null)
            {
                return;
            }

            armyInfoHUDPCView.m_InfoButton.Interactable = isInteractable;
            if (armyInfoHUDPCView.m_LeaderView?.ViewModel != null)
            {
                armyInfoHUDPCView.m_LeaderView.m_LevelupButton.Interactable = isInteractable;
            }
        }

        private void UpdateGlobalMapMenuView(bool isInteractable)
        {
            var globalMapMenuPCView = _uiAccessor.GlobalMapMenuPCView;
            if (globalMapMenuPCView?.ViewModel == null)
            {
                return;
            }

            globalMapMenuPCView.m_KingdomButton.Interactable = isInteractable;
            globalMapMenuPCView.m_RecruitButton.Interactable = isInteractable;

            globalMapMenuPCView.m_RestButton.Interactable = isInteractable;
            globalMapMenuPCView.m_GroupManagerButton.Interactable = isInteractable;
            globalMapMenuPCView.m_SkipTime.Interactable = isInteractable;
        }

        private void UpdateGlobalMapArmyMarkers(bool isInteractable)
        {
            var markerPCView = _uiAccessor.GlobalMapArmyPointerMarkerPCView;
            if (markerPCView?.m_Items != null)
            {
                foreach (var marker in markerPCView.m_Items.Values)
                {
                    marker.m_Button.Interactable = isInteractable;
                }
            }
        }

        private void UpdateGlobalMapMovementButtons(bool isInteractable)
        {
            if (GlobalMapUI.Instance == null)
            {
                return;
            }

            GlobalMapUI.Instance.m_BtnContinue.GetComponentInChildren<OwlcatButton>().Interactable = isInteractable;
            GlobalMapUI.Instance.m_BtnStop.GetComponentInChildren<OwlcatButton>().Interactable = isInteractable;
        }

        private void DisableRecruitUI(RecruitPCView view)
        {
            foreach (var recruitVM in view.ViewModel.m_Shop)
            {
                recruitVM.CanBuy.Value = false;
            }

            foreach (var mercVM in view.ViewModel.m_MercShop)
            {
                mercVM.CanBuy.Value = false;
            }

            var leaderView = view.m_ArmyView.m_LeaderInfoView;
            if (leaderView?.ViewModel != null)
            {
                leaderView.m_EmptyButton.Interactable = false;
                leaderView.m_Button.Interactable = false;
            }
        }

        private void UseSpell(BlueprintGlobalMagicSpell.GlobalMagicData context, BlueprintGlobalMagicSpell spell, GlobalMapArmyState armyState, GlobalMapPointState pointState)
        {
            // unified logic of Kingmaker.Crusade.GlobalMagic.Executors
            context.Setup(spell, armyState, pointState);
            context.Actions.Run();
            var fxTarget = context.TargetArmy?.View.gameObject ?? GlobalMapView.Instance.PlayerPawn.gameObject;
            context.BlueprintSpell.Executor.ApplyFX(fxTarget);
            if (context.TargetPoint != null)
            {
                context.BlueprintSpell.Executor.ApplyFX(context.TargetPoint.View.gameObject);
            }
        }

        private void UpdateRecruitmentUnits(List<RecruitShopUnitView> recruitUnits, bool isInteractable)
        {
            foreach (var unit in recruitUnits)
            {
                if (!isInteractable)
                {
                    unit.ViewModel.CanBuy.Value = false;
                    continue;
                }

                unit.ViewModel.UpdateValues(unit.ViewModel.Recruit);
            }
        }

        private ArmyInfoArmyCartView GetArmyInfoCart(NetworkGlobalMapArmy globalMapArmy)
        {
            List<ArmyInfoArmyCartView> views = [_uiAccessor.ArmyInfoPCView?.m_MainArmyCartView, _uiAccessor.ArmyInfoPCView?.m_MergeArmyCartView, _uiAccessor.RecruitPCView?.m_ArmyView];
            var view = views.FirstOrDefault(x => x?.ViewModel != null && string.Equals(x.ViewModel.State.Id, globalMapArmy.Id, StringComparison.OrdinalIgnoreCase));
            return view;
        }

        private void UpdateCrusadeArmySetLeaderUI(ArmyCartSetLeaderView view, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (view?.ViewModel == null)
                {
                    return;
                }

                var pcView = (ArmyCartSetLeaderPCView)view;
                pcView.m_CloseButton.Interactable = isInteractable;
                pcView.m_ClearLeaderButton.Interactable = isInteractable;
                pcView.m_RecruitNewLeaderButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(view.m_ClearLeaderButtonText, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(view.m_RecruitNewLeaderButtonText, readyPlayersCount, totalPlayersCount);

                UpdateLeaderInfoUIState((ArmyLeaderInfoPCView)view.m_LeaderInfoView, isInteractable, readyPlayersCount, totalPlayersCount);
                foreach (var leader in view.Leaders)
                {
                    UpdateLeaderInfoUIState((ArmyLeaderInfoPCView)leader, isInteractable, readyPlayersCount, totalPlayersCount);
                }

                _logger.LogInformation("Crusade army info ui has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        private void UpdateLeaderInfoUIState(ArmyLeaderInfoPCView view, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            if (view?.ViewModel == null)
            {
                return;
            }

            view.m_LevelUpButton.Interactable = isInteractable;
            view.m_Button.Interactable = isInteractable;
            _uiSyncCountersService.UpdateButtonTextCounter(view.m_ButtonText, readyPlayersCount, totalPlayersCount);

            view.m_EmptyButton.Interactable = isInteractable;
            _uiSyncCountersService.UpdateButtonTextCounter(view.m_EmptyButtonText, readyPlayersCount, totalPlayersCount);
        }

        private void UpdateArmyCartView(ArmyInfoArmyCartView armyInfoArmyCartView, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            if (armyInfoArmyCartView?.ViewModel == null)
            {
                return;
            }

            var view = (ArmyInfoArmyCartPCView)armyInfoArmyCartView;
            if (armyInfoArmyCartView?.ViewModel != null)
            {
                view.m_CloseButton.Interactable = isInteractable;
                view.m_DismissButton.Interactable = isInteractable;
                view.m_Header.interactable = isInteractable;

                UpdateLeaderInfoUIState((ArmyLeaderInfoPCView)view.m_LeaderInfoView, isInteractable, readyPlayersCount, totalPlayersCount);
            }
        }

        private (ArmySquadsPCView view, ArmyInfoSquadVM squadVM)? GetArmyInfoSquadVM(ArmySquadsPCView armySquadsView, NetworkGlobalMapArmySquadSlot mapArmySquadSlot)
        {
            if (armySquadsView?.ViewModel == null)
            {
                return null;
            }

            var position = new Vector2Int(mapArmySquadSlot.Position.X, mapArmySquadSlot.Position.Y);
            var squadView = armySquadsView?.m_Squads.FirstOrDefault(v => v.ViewModel != null && string.Equals(v.ViewModel.Army.ArmyStateId, mapArmySquadSlot.ArmyId, StringComparison.OrdinalIgnoreCase) && v.ViewModel.SquadPosition == position);
            if (squadView?.ViewModel == null)
            {
                return null;
            }

            return (armySquadsView, squadView.ViewModel);
        }

        private (ArmySquadsPCView view, ArmyInfoSquadVM) GetArmyInfoSquadVM(NetworkGlobalMapArmySquadSlot mapArmySquadSlot)
        {
            var squad = GetArmyInfoSquadVM((ArmySquadsPCView)_uiAccessor.ArmyInfoPCView?.m_MainArmyCartView?.m_SquadsView, mapArmySquadSlot)
                ?? GetArmyInfoSquadVM((ArmySquadsPCView)_uiAccessor.ArmyInfoPCView?.m_MergeArmyCartView?.m_SquadsView, mapArmySquadSlot)
                ?? GetArmyInfoSquadVM((ArmySquadsPCView)_uiAccessor.ArmyInfoHUDPCView?.m_SquadView, mapArmySquadSlot);

            if (squad == null)
            {
                return (null, null);
            }

            return squad.Value;
        }

        /// <summary>
        /// It's impossible to have same leader on multiple views, so it's fine to loop through every view
        /// </summary>
        /// <param name="globalMapArmyLeader"></param>
        /// <returns></returns>
        private ArmyLeaderInfoVM GetArmyLeaderView(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType armyLeaderActionType)
        {
            var armyInfo = _uiAccessor.ArmyInfoPCView;
            var mainCartView = armyInfo?.m_MainArmyCartView?.m_LeaderInfoView;
            var mergeCartView = armyInfo?.m_MergeArmyCartView?.m_LeaderInfoView;
            var recruitmentView = _uiAccessor.RecruitPCView;
            if (globalMapArmyLeader == null)
            {
                switch (armyLeaderActionType)
                {
                    case NetworkGlobalMapArmyLeaderActionType.MainLookAtPool when mainCartView?.ViewModel != null:
                        return mainCartView.ViewModel;
                    case NetworkGlobalMapArmyLeaderActionType.MergeLookAtPool when mergeCartView?.ViewModel != null:
                        return mergeCartView.ViewModel;
                    case NetworkGlobalMapArmyLeaderActionType.MergeLookAtPool when recruitmentView?.m_ArmyView?.m_LeaderInfoView?.ViewModel != null:
                        return recruitmentView.m_ArmyView.m_LeaderInfoView.ViewModel;
                    default:
                        _logger.LogError("GlobalMapArmyLeader is null. Type={Type}", armyLeaderActionType);
                        return null;
                }
            }

            // BuyLeaders screen is always on top
            List<ArmyLeaderInfoView> viewsToSearch = [.. _uiAccessor.ArmyCartBuyLeaderPCView.m_Leaders, mainCartView, mergeCartView, .. armyInfo.m_SetLeaderView.Leaders,
                recruitmentView.m_ArmyView.m_LeaderInfoView, .. recruitmentView.m_LeaderSetView.Leaders];

            var viewModel = viewsToSearch.FirstOrDefault(v => IsArmyLeaderViewMatched(v, globalMapArmyLeader))?.ViewModel;

            if (viewModel == null && armyLeaderActionType == NetworkGlobalMapArmyLeaderActionType.LevelUp)
            {
                return _uiAccessor.ArmyInfoHUDPCView?.m_LeaderView?.ViewModel;
            }

            return viewModel;
        }

        private bool IsArmyLeaderViewMatched(ArmyLeaderInfoView view, NetworkGlobalMapArmyLeader globalMapArmyLeader)
        {
            return view?.ViewModel != null
                && view.ViewModel.m_Leader != null
                && string.Equals(view.ViewModel.m_Leader.Guid, globalMapArmyLeader.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(view.ViewModel.m_Leader.Blueprint.AssetGuid.ToString(), globalMapArmyLeader.BlueprintId, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateTraveler(NetworkGlobalMapTraveler traveler)
        {
            var selectedTraveler = Game.Instance.GlobalMapController.SelectedTraveler;
            UpdateTraveler(selectedTraveler, traveler);
        }

        private void UpdateTraveler(IGlobalMapTraveler globalMapTraveler, NetworkGlobalMapTraveler traveler)
        {
            if (globalMapTraveler == null || traveler == null)
            {
                return;
            }

            if (globalMapTraveler is GlobalMapArmyState armyState && traveler.MovementPoints.HasValue && armyState.MovementPoints != traveler.MovementPoints.Value)
            {
                _logger.LogInformation("Updated army movement points. ArmyId={ArmyId}, OldValue={OldValue}, NewValue={NewValue}", armyState.Id, armyState.MovementPoints, traveler.MovementPoints.Value);
                armyState.MovementPoints = traveler.MovementPoints.Value;
            }

            if (globalMapTraveler.Position.EdgePosition != traveler.Position.EdgePosition)
            {
                _logger.LogInformation("Updated traveler edge position. OldValue={OldValue}, NewValue={NewValue}", globalMapTraveler.Position.EdgePosition, traveler.Position.EdgePosition);
                globalMapTraveler.Position.EdgePosition = traveler.Position.EdgePosition;
            }
        }
    }
}
